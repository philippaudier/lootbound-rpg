using System;
using UnityEngine;
using UnityEngine.AI;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Enemy AI orchestrator. Composes the single state machine
    /// (EnemyStateMachine), perception (EnemyPerception), a roaming behaviour
    /// (IEnemyRoamingBehaviour: wander or patrol) and the NavMeshAgent - the
    /// agent IS the motor, no extra layer around it.
    ///
    /// Territory: HomePosition is captured after the final NavMesh placement
    /// (post spawner Warp), never from the reservation or prefab position.
    /// No assumption about nodes or paths: any navigable start position works.
    ///
    /// Perception decides what is seen; the brain decides the state; the
    /// NavMeshAgent moves. Combat states (windup/active/recovery) keep their
    /// existing contract with EnemyCombat via OnStateChanged.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBrain : MonoBehaviour
    {
        private const string Category = "EnemyBrain";

        // Retry pacing inside the Stuck state.
        private const float StuckRetryInterval = 1f;

        [Header("Configuration")]
        [SerializeField] private EnemyConfig config;

        [Header("Target")]
        [SerializeField] private Transform target;

        private NavMeshAgent agent;
        private EnemyHealth health;

        private EnemyStateMachine stateMachine;
        private EnemyPerception perception;
        private IEnemyRoamingBehaviour roaming;
        private EnemyNavigationProfile profile;
        private System.Random instanceRandom;

        private bool initialized;
        private float attackCooldownTimer;
        private float reacquireBlockedUntil = float.NegativeInfinity;

        // Chase repath throttling
        private float lastRepathTime;
        private Vector3 lastRepathTargetPosition;

        // Stuck tracking
        private float lastProgressTime;
        private Vector3 lastProgressPosition;
        private int recoveryAttempts;

        // Bounded no-sight-required chase opened by taking damage.
        private readonly EnemyDefensiveChase defensiveChase = new EnemyDefensiveChase();

        private static EnemyNavigationProfile sharedDefaultProfile;

        #region Public API (consumed by EnemyCombat, debug, tests)

        public EnemyState CurrentState => stateMachine?.CurrentState ?? EnemyState.Idle;
        public EnemyState PreviousState => stateMachine?.PreviousState ?? EnemyState.Idle;
        public EnemyTransitionReason LastTransitionReason => stateMachine?.LastReason ?? EnemyTransitionReason.None;
        public float CurrentStateDuration => stateMachine?.StateDuration(Time.time) ?? 0f;

        public Transform Target => target;

        public float DistanceToTarget => target != null
            ? Vector3.Distance(transform.position, target.position)
            : float.MaxValue;

        /// <summary>Territory anchor captured after final NavMesh placement.</summary>
        public Vector3 HomePosition { get; private set; }

        public float DistanceFromHome => initialized
            ? Vector3.Distance(transform.position, HomePosition)
            : 0f;

        public bool IsInitialized => initialized;
        public bool TargetVisible => perception != null && perception.TargetVisible;
        public float TimeSinceTargetSeen => perception != null ? perception.TimeSinceSeen(Time.time) : float.PositiveInfinity;
        public string RoamingModeName => roaming?.ModeName ?? "-";
        public int RecoveryCount { get; private set; }
        public int EmergencyWarpCount { get; private set; }
        public float TimeWithoutProgress => agent != null && agent.hasPath ? Time.time - lastProgressTime : 0f;
        public Vector3 CurrentDestination => agent != null && agent.hasPath ? agent.destination : transform.position;
        public EnemyNavigationProfile Profile => profile;

        public string PathStatusText
        {
            get
            {
                if (agent == null || !agent.enabled) return "Disabled";
                if (agent.pathPending) return "Pending";
                if (!agent.hasPath) return "None";
                return agent.pathStatus.ToString();
            }
        }

        /// <summary>Fired when state changes (old, new).</summary>
        public event Action<EnemyState, EnemyState> OnStateChanged;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();

            stateMachine = new EnemyStateMachine(EnemyState.Idle, Time.time);
            stateMachine.OnTransition += HandleTransition;

            // Auto-find player if no target assigned
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        private void Start()
        {
            profile = ResolveProfile();

            if (config != null && agent != null)
            {
                agent.speed = config.MoveSpeed * profile.RoamingSpeedMultiplier;
                agent.angularSpeed = config.TurnSpeed;
            }

            if (health != null)
            {
                health.OnStagger += HandleStagger;
                health.OnDied += HandleDeath;
                health.OnDamaged += HandleDamaged;
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnStagger -= HandleStagger;
                health.OnDied -= HandleDeath;
                health.OnDamaged -= HandleDamaged;
            }

            if (stateMachine != null)
            {
                stateMachine.OnTransition -= HandleTransition;
            }
        }

        private void Update()
        {
            if (CurrentState == EnemyState.Dead)
            {
                return;
            }

            float now = Time.time;

            if (!initialized)
            {
                TryInitialize(now);
                return;
            }

            if (attackCooldownTimer > 0f)
            {
                attackCooldownTimer -= Time.deltaTime;
            }

            perception.Tick(now);

            switch (CurrentState)
            {
                case EnemyState.Idle:
                    UpdateIdle(now);
                    break;
                case EnemyState.Wandering:
                case EnemyState.Patrolling:
                    UpdateRoamingMove(now);
                    break;
                case EnemyState.Suspicious:
                    UpdateSuspicious(now);
                    break;
                case EnemyState.Chasing:
                    UpdateChasing(now);
                    break;
                case EnemyState.AttackWindup:
                    UpdateAttackWindup(now);
                    break;
                case EnemyState.AttackActive:
                    UpdateAttackActive(now);
                    break;
                case EnemyState.AttackRecovery:
                    UpdateAttackRecovery(now);
                    break;
                case EnemyState.ReturningHome:
                    UpdateReturningHome(now);
                    break;
                case EnemyState.Stagger:
                    UpdateStagger(now);
                    break;
                case EnemyState.Stuck:
                    UpdateStuck(now);
                    break;
            }
        }

        /// <summary>
        /// Capture the territory once the agent stands on the final NavMesh
        /// (the spawner warps the agent before our first Update, but a guard
        /// keeps manually placed enemies safe too).
        /// </summary>
        private void TryInitialize(float now)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            HomePosition = transform.position;
            instanceRandom = CreateInstanceRandom();
            perception = new EnemyPerception(
                transform, target, config, profile,
                now + profile.PerceptionInterval * (float)instanceRandom.NextDouble());
            roaming = CreateRoamingBehaviour();
            roaming.OnRoamingResumed(now);
            ResetProgressTracking(now);
            initialized = true;
        }

        #endregion

        #region State updates

        private void UpdateIdle(float now)
        {
            if (TryNoticeTarget(now))
            {
                return;
            }

            if (roaming.TryGetNextDestination(HomePosition, transform.position, now, out Vector3 destination))
            {
                if (agent.SetDestination(destination))
                {
                    ChangeState(roaming.MovingState, EnemyTransitionReason.RoamingStarted);
                }
            }
        }

        private void UpdateRoamingMove(float now)
        {
            if (TryNoticeTarget(now))
            {
                return;
            }

            if (HasArrivedAtDestination(profile.ArrivalDistance))
            {
                roaming.OnDestinationReached(now);
                ChangeState(EnemyState.Idle, EnemyTransitionReason.DestinationReached);
                return;
            }

            CheckStuckAndRecover(now, EnemyState.Idle);
        }

        private void UpdateSuspicious(float now)
        {
            // Observing, never moving: the hesitation is what makes the
            // upcoming chase readable.
            FaceTarget();

            if (perception.TargetVisible)
            {
                bool confirmed = stateMachine.StateDuration(now) >= profile.SuspicionDuration;
                bool inLeash = target != null && EnemyPursuitRules.CanStartChase(
                    Vector3.Distance(target.position, HomePosition),
                    profile.MaxChaseDistanceFromHome,
                    profile.LeashHysteresis);

                if (confirmed && inLeash)
                {
                    ChangeState(EnemyState.Chasing, EnemyTransitionReason.SuspicionConfirmed);
                }
                // Visible but outside the territory leash: keep watching from here.
                return;
            }

            // Target vanished before the suspicion was confirmed.
            if (stateMachine.StateDuration(now) >= profile.SuspicionDuration)
            {
                // Far from the territory (interrupted return, post-combat):
                // walk back instead of wandering around a distant home.
                if (DistanceFromHome > profile.WanderRadius)
                {
                    StartReturningHome(EnemyTransitionReason.TargetLost);
                }
                else
                {
                    ResumeRoaming(now, EnemyTransitionReason.TargetLost);
                }
            }
        }

        private void UpdateChasing(float now)
        {
            if (target == null)
            {
                StartReturningHome(EnemyTransitionReason.TargetLost);
                return;
            }

            float targetDistanceFromHome = Vector3.Distance(target.position, HomePosition);
            // While the defensive window is active the chase needs no line of
            // sight (the enemy was hit); the territorial leash always applies.
            float effectiveTimeSinceSeen = defensiveChase.IsActive(now) ? 0f : perception.TimeSinceSeen(now);
            if (EnemyPursuitRules.ShouldAbandonChase(
                    targetDistanceFromHome,
                    profile.MaxChaseDistanceFromHome,
                    effectiveTimeSinceSeen,
                    profile.LoseSightDelay))
            {
                var reason = targetDistanceFromHome > profile.MaxChaseDistanceFromHome
                    ? EnemyTransitionReason.LeashExceeded
                    : EnemyTransitionReason.LostSightTimeout;
                StartReturningHome(reason);
                return;
            }

            float distance = DistanceToTarget;

            // In attack range and ready - hand over to the combat cycle.
            if (distance <= config.AttackRange && attackCooldownTimer <= 0f)
            {
                StopAgent();
                ChangeState(EnemyState.AttackWindup, EnemyTransitionReason.AttackStarted);
                return;
            }

            // In attack range but on cooldown - hold position, face the player.
            if (distance <= config.AttackRange)
            {
                StopAgent();
                FaceTarget();
                return;
            }

            // Pursue with throttled repathing (no per-frame destination churn).
            Vector3 pursuitPoint = perception.TargetVisible ? target.position : perception.LastKnownTargetPosition;
            bool intervalElapsed = now - lastRepathTime >= profile.ChaseRepathInterval;
            bool movedEnough = (pursuitPoint - lastRepathTargetPosition).sqrMagnitude >=
                               profile.ChaseRepathDistance * profile.ChaseRepathDistance;
            if (intervalElapsed && (movedEnough || !agent.hasPath))
            {
                agent.SetDestination(pursuitPoint);
                lastRepathTime = now;
                lastRepathTargetPosition = pursuitPoint;
            }

            CheckStuckAndRecover(now, EnemyState.ReturningHome);
        }

        private void UpdateAttackWindup(float now)
        {
            StopAgent();
            FaceTarget();

            if (stateMachine.StateDuration(now) >= config.AttackWindup)
            {
                ChangeState(EnemyState.AttackActive, EnemyTransitionReason.AttackPhase);
            }
        }

        private void UpdateAttackActive(float now)
        {
            // Limited tracking during the active phase
            if (config.TurnSpeed > 0f)
            {
                FaceTarget(0.5f);
            }

            if (stateMachine.StateDuration(now) >= config.AttackActive)
            {
                ChangeState(EnemyState.AttackRecovery, EnemyTransitionReason.AttackPhase);
            }
        }

        private void UpdateAttackRecovery(float now)
        {
            if (stateMachine.StateDuration(now) >= config.AttackRecovery)
            {
                attackCooldownTimer = config.AttackCooldown;
                ReevaluateAfterInterruption(EnemyTransitionReason.AttackFinished);
            }
        }

        private void UpdateReturningHome(float now)
        {
            // Long-range passive reacquisition is deliberately disabled during
            // the return - but the enemy is not blind at point-blank range.
            if (perception.TargetWithinAwareness)
            {
                ChangeState(EnemyState.Suspicious, EnemyTransitionReason.TargetSpotted);
                return;
            }

            if (HasArrivedAtDestination(profile.ReturnCompletionDistance) ||
                EnemyPursuitRules.HasArrived(DistanceFromHome, profile.ReturnCompletionDistance))
            {
                reacquireBlockedUntil = now + profile.ReacquireCooldown;
                ResumeRoaming(now, EnemyTransitionReason.ArrivedHome);
                return;
            }

            CheckStuckAndRecover(now, EnemyState.Stuck);
        }

        private void UpdateStagger(float now)
        {
            // Agent is disabled during stagger to allow knockback physics.
            if (stateMachine.StateDuration(now) >= config.StaggerDuration)
            {
                if (!agent.enabled)
                {
                    agent.enabled = true;
                }

                // Re-evaluate the world instead of blindly chasing.
                ReevaluateAfterInterruption(EnemyTransitionReason.StaggerRecovered);
            }
        }

        private void UpdateStuck(float now)
        {
            if (stateMachine.StateDuration(now) < StuckRetryInterval * (recoveryAttempts + 1))
            {
                return;
            }

            recoveryAttempts++;
            RecoveryCount++;

            // Try to find navigable ground near home and walk there.
            if (SampleNavMesh(HomePosition, profile.WanderSampleDistance * 2f, out Vector3 nearHome) &&
                agent.isOnNavMesh && agent.SetDestination(nearHome))
            {
                ResetProgressTracking(now);
                ChangeState(EnemyState.ReturningHome, EnemyTransitionReason.StuckRecovered);
                return;
            }

            if (recoveryAttempts < profile.MaxRecoveryAttempts)
            {
                return;
            }

            // Last resort, rare, always logged, configurable.
            if (profile.AllowEmergencyWarp &&
                SampleNavMesh(HomePosition, Mathf.Max(profile.WanderRadius, 10f), out Vector3 warpPoint) &&
                agent.Warp(warpPoint))
            {
                EmergencyWarpCount++;
                LootboundLog.Warning(Category,
                    $"{gameObject.name}: emergency warp to home area after {recoveryAttempts} failed recoveries " +
                    $"(total warps: {EmergencyWarpCount})");
                ResumeRoaming(now, EnemyTransitionReason.StuckRecovered);
                return;
            }

            // Give up cleanly: live where we stand; wander keeps pulling
            // toward HomePosition by construction.
            LootboundLog.Warning(Category, $"{gameObject.name}: stuck recovery failed, resuming roaming in place");
            ResumeRoaming(now, EnemyTransitionReason.StuckRecovered);
        }

        #endregion

        #region Decisions and transitions

        /// <summary>
        /// Shared "player noticed" check for roaming states.
        /// </summary>
        private bool TryNoticeTarget(float now)
        {
            if (!perception.TargetVisible)
            {
                return false;
            }

            if (!EnemyPursuitRules.CanReacquireTarget(now, reacquireBlockedUntil))
            {
                return false;
            }

            return ChangeState(EnemyState.Suspicious, EnemyTransitionReason.TargetSpotted);
        }

        /// <summary>
        /// After an interruption (attack recovery, stagger): look at the world
        /// again instead of forcing a chase. An active defensive window keeps
        /// the pursuit alive - a hit that staggers the enemy must not cancel
        /// its riposte.
        /// </summary>
        private void ReevaluateAfterInterruption(EnemyTransitionReason reason)
        {
            if (perception.TargetVisible || defensiveChase.IsActive(Time.time))
            {
                ChangeState(EnemyState.Chasing, reason);
            }
            else
            {
                ChangeState(EnemyState.Suspicious, reason);
            }
        }

        private void StartReturningHome(EnemyTransitionReason reason)
        {
            ChangeState(EnemyState.ReturningHome, reason);
        }

        private void ResumeRoaming(float now, EnemyTransitionReason reason)
        {
            roaming.OnRoamingResumed(now);
            ChangeState(EnemyState.Idle, reason);
        }

        private bool ChangeState(EnemyState newState, EnemyTransitionReason reason)
        {
            return stateMachine.TryTransition(newState, reason, Time.time);
        }

        /// <summary>
        /// Single place where transitions apply their side effects
        /// (speeds, stops) and get logged/forwarded.
        /// </summary>
        private void HandleTransition(EnemyStateTransition transition)
        {
            ResetProgressTracking(transition.Time);
            recoveryAttempts = 0;

            // The defensive (no-sight) window survives combat interruptions
            // (stagger, attack phases) but never leaks past the end of the
            // engagement into a later, legitimate pursuit.
            if (transition.Current == EnemyState.ReturningHome || transition.Current == EnemyState.Idle)
            {
                defensiveChase.Clear();
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh && profile != null && config != null)
            {
                switch (transition.Current)
                {
                    case EnemyState.Idle:
                    case EnemyState.Suspicious:
                        StopAgent();
                        agent.speed = config.MoveSpeed * profile.RoamingSpeedMultiplier;
                        break;

                    case EnemyState.Wandering:
                    case EnemyState.Patrolling:
                        agent.speed = config.MoveSpeed * profile.RoamingSpeedMultiplier;
                        break;

                    case EnemyState.Chasing:
                        agent.speed = config.MoveSpeed * profile.ChaseSpeedMultiplier;
                        lastRepathTime = float.NegativeInfinity;
                        break;

                    case EnemyState.ReturningHome:
                        agent.speed = config.MoveSpeed * profile.ReturnSpeedMultiplier;
                        agent.SetDestination(HomePosition);
                        break;
                }
            }

            LootboundLog.Info(Category, $"{gameObject.name}: {transition}");
            OnStateChanged?.Invoke(transition.Previous, transition.Current);
        }

        #endregion

        #region Movement helpers

        private void StopAgent()
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }

        private bool HasArrivedAtDestination(float tolerance)
        {
            return agent.enabled && agent.isOnNavMesh && !agent.pathPending &&
                   agent.remainingDistance <= tolerance;
        }

        private void FaceTarget(float speedMultiplier = 1f)
        {
            if (target == null)
            {
                return;
            }

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    config.TurnSpeed * speedMultiplier * Time.deltaTime
                );
            }
        }

        private void CheckStuckAndRecover(float now, EnemyState escalateTo)
        {
            if (!agent.enabled || !agent.hasPath || agent.pathPending)
            {
                ResetProgressTracking(now);
                return;
            }

            bool moving = agent.velocity.sqrMagnitude >
                          profile.StuckVelocityThreshold * profile.StuckVelocityThreshold;
            bool displaced = (transform.position - lastProgressPosition).sqrMagnitude > 0.05f;

            if (moving || displaced)
            {
                ResetProgressTracking(now);
                return;
            }

            if (now - lastProgressTime < profile.StuckTimeout)
            {
                return;
            }

            recoveryAttempts++;
            RecoveryCount++;
            ResetProgressTracking(now);

            if (recoveryAttempts == 1)
            {
                // First response: recompute the same destination.
                agent.SetDestination(agent.destination);
                return;
            }

            if (recoveryAttempts <= profile.MaxRecoveryAttempts &&
                SampleNavMesh(transform.position + (agent.destination - transform.position).normalized * 2f,
                    profile.WanderSampleDistance, out Vector3 nearby))
            {
                // Second response: try navigable ground just ahead.
                agent.SetDestination(nearby);
                return;
            }

            ChangeState(escalateTo, EnemyTransitionReason.StuckDetected);
        }

        private void ResetProgressTracking(float now)
        {
            lastProgressTime = now;
            lastProgressPosition = transform.position;
        }

        private static bool SampleNavMesh(Vector3 position, float maxDistance, out Vector3 sampled)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                sampled = hit.position;
                return true;
            }

            sampled = default;
            return false;
        }

        #endregion

        #region Setup helpers

        private EnemyNavigationProfile ResolveProfile()
        {
            if (config != null && config.NavigationProfile != null)
            {
                return config.NavigationProfile;
            }

            if (sharedDefaultProfile == null)
            {
                sharedDefaultProfile = ScriptableObject.CreateInstance<EnemyNavigationProfile>();
                sharedDefaultProfile.hideFlags = HideFlags.HideAndDontSave;
            }

            return sharedDefaultProfile;
        }

        /// <summary>
        /// Per-instance random source seeded from stable world data
        /// (WorldSeed + ReservationId + entry index), so enemies desynchronize
        /// from each other without touching UnityEngine.Random or the world's
        /// procedural determinism. Manually placed enemies (test scenes) fall
        /// back to stable placement data (name + rounded position).
        /// </summary>
        private System.Random CreateInstanceRandom()
        {
            var identity = GetComponent<WorldContentIdentity>();

            unchecked
            {
                uint hash = 2166136261u;

                void Mix(int value)
                {
                    hash = (hash ^ (uint)value) * 16777619u;
                }

                void MixString(string text)
                {
                    if (string.IsNullOrEmpty(text)) return;
                    foreach (char c in text) Mix(c);
                }

                if (identity != null && !string.IsNullOrEmpty(identity.ReservationId))
                {
                    Mix(identity.WorldSeed);
                    MixString(identity.ReservationId);
                    Mix(identity.EntryIndex);
                }
                else
                {
                    MixString(gameObject.name);
                    Mix(Mathf.RoundToInt(transform.position.x * 10f));
                    Mix(Mathf.RoundToInt(transform.position.z * 10f));
                }

                return new System.Random((int)hash);
            }
        }

        private IEnemyRoamingBehaviour CreateRoamingBehaviour()
        {
            if (profile.RoamingMode == EnemyRoamingMode.Patrol)
            {
                var route = GetComponent<EnemyPatrolRoute>();
                if (route != null && route.PointCount > 0)
                {
                    return new EnemyPatrolBehaviour(
                        route.ResolveWorldPoints(),
                        route.PingPong,
                        route.DwellSeconds,
                        profile.WanderSampleDistance,
                        instanceRandom,
                        SampleNavMesh);
                }

                LootboundLog.Warning(Category,
                    $"{gameObject.name}: Patrol mode selected but no EnemyPatrolRoute with points found - wandering instead");
            }

            return new EnemyWanderBehaviour(profile.ToWanderSettings(), instanceRandom, SampleNavMesh);
        }

        #endregion

        #region Health events

        /// <summary>
        /// Taking damage always alerts a peaceful enemy - ReturningHome can
        /// never be exploited as a free-hit window. Bypasses the reacquire
        /// cooldown by design. Within the territorial leash the response is a
        /// bounded defensive chase (no line of sight required, short duration,
        /// successive hits never extend it); an unreachable attacker (beyond
        /// the leash) is faced from Suspicious instead - no boundary ping-pong.
        /// </summary>
        private void HandleDamaged(DamageRequest request)
        {
            if (!initialized || target == null)
            {
                return;
            }

            var state = CurrentState;
            bool peaceful = state == EnemyState.Idle || state == EnemyState.Wandering ||
                            state == EnemyState.Patrolling || state == EnemyState.Suspicious ||
                            state == EnemyState.ReturningHome;
            if (!peaceful)
            {
                return;
            }

            float now = Time.time;
            var reason = state == EnemyState.ReturningHome
                ? EnemyTransitionReason.AttackedWhileReturning
                : EnemyTransitionReason.TargetSpotted;

            float targetDistanceFromHome = Vector3.Distance(target.position, HomePosition);
            if (targetDistanceFromHome <= profile.MaxChaseDistanceFromHome)
            {
                defensiveChase.TryStart(now, profile.DefensiveChaseDuration);
                // Head for the attacker, not for a stale last-seen point.
                perception.NotifyTargetPosition(target.position);
                ChangeState(EnemyState.Chasing, reason);
            }
            else if (state != EnemyState.Suspicious)
            {
                // Attacker outside the territory: face the threat, stand ground.
                ChangeState(EnemyState.Suspicious, reason);
            }
        }

        private void HandleStagger(float force)
        {
            // Stagger can interrupt movement and windup, but not the active attack.
            var state = CurrentState;
            if (state != EnemyState.AttackActive && state != EnemyState.Stagger && state != EnemyState.Dead)
            {
                // Disable agent to allow knockback physics
                agent.enabled = false;
                ChangeState(EnemyState.Stagger, EnemyTransitionReason.Staggered);
            }
        }

        private void HandleDeath()
        {
            ChangeState(EnemyState.Dead, EnemyTransitionReason.Died);
            agent.enabled = false;
        }

        #endregion

        #region Runtime configuration

        /// <summary>Set target at runtime.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (perception != null)
            {
                perception.Target = newTarget;
            }
        }

        /// <summary>Set config at runtime.</summary>
        public void SetConfig(EnemyConfig newConfig)
        {
            config = newConfig;
            profile = ResolveProfile();
            if (config != null && agent != null)
            {
                agent.speed = config.MoveSpeed * profile.RoamingSpeedMultiplier;
                agent.angularSpeed = config.TurnSpeed;
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!initialized || profile == null)
            {
                return;
            }

            // Territory: home, wander radius, chase leash.
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(HomePosition + Vector3.up * 0.2f, 0.4f);
            DrawGizmoCircle(HomePosition, profile.WanderRadius, new Color(0.2f, 1f, 0.4f, 0.6f));
            DrawGizmoCircle(HomePosition, profile.MaxChaseDistanceFromHome, new Color(1f, 0.55f, 0.1f, 0.6f));

            // Vision cone
            if (config != null)
            {
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
                float halfFov = config.FieldOfView * 0.5f;
                Vector3 left = Quaternion.Euler(0f, -halfFov, 0f) * transform.forward;
                Vector3 right = Quaternion.Euler(0f, halfFov, 0f) * transform.forward;
                Gizmos.DrawRay(transform.position + Vector3.up * profile.EyeHeight, left * config.DetectionRange);
                Gizmos.DrawRay(transform.position + Vector3.up * profile.EyeHeight, right * config.DetectionRange);
                DrawGizmoCircle(transform.position, profile.ImmediateDetectionRange, new Color(1f, 0.9f, 0.2f, 0.5f));
            }

            // Current destination
            if (agent != null && agent.enabled && agent.hasPath)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(transform.position, agent.destination);
                Gizmos.DrawWireSphere(agent.destination, 0.25f);
            }
        }

        private static void DrawGizmoCircle(Vector3 center, float radius, Color color, int segments = 40)
        {
            Gizmos.color = color;
            Vector3 previous = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        #endregion
    }
}
