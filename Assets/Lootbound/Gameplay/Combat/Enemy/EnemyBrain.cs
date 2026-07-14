using System;
using UnityEngine;
using UnityEngine.AI;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Simple state machine AI for enemy behavior.
    /// Handles detection, chase, attack, and stagger states.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBrain : MonoBehaviour
    {
        private const string Category = "EnemyBrain";

        [Header("Configuration")]
        [SerializeField] private EnemyConfig config;

        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Debug")]
        [SerializeField] private bool debugDraw = false;

        private NavMeshAgent agent;
        private EnemyHealth health;
        private EnemyCombat combat;

        private EnemyState currentState = EnemyState.Idle;
        private float stateTimer;
        private float attackCooldownTimer;

        /// <summary>
        /// Current AI state.
        /// </summary>
        public EnemyState CurrentState => currentState;

        /// <summary>
        /// Current target transform.
        /// </summary>
        public Transform Target => target;

        /// <summary>
        /// Distance to target (or infinity if no target).
        /// </summary>
        public float DistanceToTarget => target != null
            ? Vector3.Distance(transform.position, target.position)
            : float.MaxValue;

        /// <summary>
        /// Fired when state changes.
        /// </summary>
        public event Action<EnemyState, EnemyState> OnStateChanged;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            combat = GetComponent<EnemyCombat>();

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
            if (config != null && agent != null)
            {
                agent.speed = config.MoveSpeed;
                agent.angularSpeed = config.TurnSpeed;
            }

            if (health != null)
            {
                health.OnStagger += HandleStagger;
                health.OnDied += HandleDeath;
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnStagger -= HandleStagger;
                health.OnDied -= HandleDeath;
            }
        }

        private void Update()
        {
            if (currentState == EnemyState.Dead)
            {
                return;
            }

            UpdateCooldowns();
            UpdateStateMachine();

            if (debugDraw)
            {
                DrawDebug();
            }
        }

        private void UpdateCooldowns()
        {
            if (attackCooldownTimer > 0f)
            {
                attackCooldownTimer -= Time.deltaTime;
            }
        }

        private void UpdateStateMachine()
        {
            stateTimer += Time.deltaTime;

            switch (currentState)
            {
                case EnemyState.Idle:
                    UpdateIdle();
                    break;

                case EnemyState.Chase:
                    UpdateChase();
                    break;

                case EnemyState.AttackWindup:
                    UpdateAttackWindup();
                    break;

                case EnemyState.AttackActive:
                    UpdateAttackActive();
                    break;

                case EnemyState.AttackRecovery:
                    UpdateAttackRecovery();
                    break;

                case EnemyState.Stagger:
                    UpdateStagger();
                    break;
            }
        }

        private void UpdateIdle()
        {
            if (target == null)
            {
                return;
            }

            if (CanSeeTarget())
            {
                ChangeState(EnemyState.Chase);
            }
        }

        private void UpdateChase()
        {
            if (target == null)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            float distance = DistanceToTarget;

            // Lost target
            if (distance > config.DetectionRange * 1.5f)
            {
                agent.SetDestination(transform.position);
                ChangeState(EnemyState.Idle);
                return;
            }

            // In attack range and ready to attack
            if (distance <= config.AttackRange && attackCooldownTimer <= 0f)
            {
                agent.SetDestination(transform.position);
                ChangeState(EnemyState.AttackWindup);
                return;
            }

            // In attack range but on cooldown - stop and wait
            if (distance <= config.AttackRange)
            {
                agent.SetDestination(transform.position);
                FaceTarget();
                return;
            }

            // Outside attack range - chase
            agent.SetDestination(target.position);
            FaceTarget();
        }

        private void UpdateAttackWindup()
        {
            // Stop moving during windup
            agent.SetDestination(transform.position);
            FaceTarget();

            if (stateTimer >= config.AttackWindup)
            {
                ChangeState(EnemyState.AttackActive);
            }
        }

        private void UpdateAttackActive()
        {
            // Limited tracking during active phase
            if (config.TurnSpeed > 0f)
            {
                FaceTarget(0.5f); // Reduced tracking
            }

            if (stateTimer >= config.AttackActive)
            {
                ChangeState(EnemyState.AttackRecovery);
            }
        }

        private void UpdateAttackRecovery()
        {
            if (stateTimer >= config.AttackRecovery)
            {
                attackCooldownTimer = config.AttackCooldown;
                ChangeState(EnemyState.Chase);
            }
        }

        private void UpdateStagger()
        {
            // Agent is disabled during stagger to allow knockback physics

            if (stateTimer >= config.StaggerDuration)
            {
                // Re-enable agent before leaving stagger
                if (!agent.enabled)
                {
                    agent.enabled = true;
                }
                ChangeState(EnemyState.Chase);
            }
        }

        private bool CanSeeTarget()
        {
            if (target == null || config == null)
            {
                return false;
            }

            float distance = DistanceToTarget;
            if (distance > config.DetectionRange)
            {
                return false;
            }

            // Check field of view
            Vector3 dirToTarget = (target.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToTarget);

            if (angle > config.FieldOfView / 2f)
            {
                return false;
            }

            // Raycast for line of sight - check if anything blocks view to target
            Vector3 eyePosition = transform.position + Vector3.up * 1.5f;
            Vector3 targetCenter = target.position + Vector3.up * 1f;
            Vector3 toTarget = targetCenter - eyePosition;

            if (Physics.Raycast(eyePosition, toTarget.normalized, out RaycastHit hit, distance))
            {
                // Check if we hit the target or something blocking
                if (!hit.transform.IsChildOf(target) && hit.transform != target)
                {
                    if (debugDraw)
                    {
                        Debug.DrawLine(eyePosition, hit.point, Color.red, 0.1f);
                    }
                    return false;
                }
            }

            if (debugDraw)
            {
                Debug.DrawLine(eyePosition, targetCenter, Color.green, 0.1f);
            }

            return true;
        }

        private void FaceTarget(float speedMultiplier = 1f)
        {
            if (target == null)
            {
                return;
            }

            Vector3 direction = (target.position - transform.position);
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

        private void ChangeState(EnemyState newState)
        {
            if (currentState == newState)
            {
                return;
            }

            var oldState = currentState;
            currentState = newState;
            stateTimer = 0f;

            LootboundLog.Info(Category, $"{gameObject.name}: {oldState} -> {newState}");
            OnStateChanged?.Invoke(oldState, newState);
        }

        private void HandleStagger(float force)
        {
            // Stagger can interrupt windup but not active attack
            if (currentState == EnemyState.AttackWindup || currentState == EnemyState.Chase || currentState == EnemyState.Idle)
            {
                // Disable agent to allow knockback physics
                agent.enabled = false;
                ChangeState(EnemyState.Stagger);
            }
        }

        private void HandleDeath()
        {
            ChangeState(EnemyState.Dead);
            agent.enabled = false;
        }

        /// <summary>
        /// Set target at runtime.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Set config at runtime.
        /// </summary>
        public void SetConfig(EnemyConfig newConfig)
        {
            config = newConfig;
            if (config != null && agent != null)
            {
                agent.speed = config.MoveSpeed;
                agent.angularSpeed = config.TurnSpeed;
            }
        }

        private void DrawDebug()
        {
            if (config == null)
            {
                return;
            }

            // Detection range
            DebugDrawCircle(transform.position, config.DetectionRange, Color.yellow);

            // Attack range
            DebugDrawCircle(transform.position, config.AttackRange, Color.red);

            // FOV
            float halfFov = config.FieldOfView / 2f;
            Vector3 leftDir = Quaternion.Euler(0, -halfFov, 0) * transform.forward;
            Vector3 rightDir = Quaternion.Euler(0, halfFov, 0) * transform.forward;
            Debug.DrawRay(transform.position, leftDir * config.DetectionRange, Color.cyan);
            Debug.DrawRay(transform.position, rightDir * config.DetectionRange, Color.cyan);
        }

        private void DebugDrawCircle(Vector3 center, float radius, Color color, int segments = 32)
        {
            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;
                Vector3 p1 = center + new Vector3(Mathf.Cos(angle1), 0, Mathf.Sin(angle1)) * radius;
                Vector3 p2 = center + new Vector3(Mathf.Cos(angle2), 0, Mathf.Sin(angle2)) * radius;
                Debug.DrawLine(p1, p2, color);
            }
        }
    }
}
