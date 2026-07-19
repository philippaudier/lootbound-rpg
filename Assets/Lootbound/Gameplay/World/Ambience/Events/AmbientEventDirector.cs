using System;
using System.Collections.Generic;
using UnityEngine;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.World.Ambience.Events
{
    /// <summary>
    /// The brain that turns ambience activity intents into temporary,
    /// world-anchored ambient events around the player. Sound belongs to the
    /// world - never to the player's head: an instance is a position with a
    /// lifetime, and presentation layers (future audio slice) subscribe to
    /// OnEventSpawned / OnEventReleased to give it a voice.
    ///
    /// Reads ONLY WorldAmbienceController.CurrentState (never Depth01 or the
    /// progression). No AudioSource, AudioClip or Shader is referenced.
    /// Evaluates at a configurable cadence, never per frame; missed time is
    /// discarded so a long silence never produces a burst of catch-up spawns.
    /// No coroutines: expiration is a time comparison in Update.
    /// </summary>
    public sealed class AmbientEventDirector : MonoBehaviour
    {
        private const string LogCategory = "AmbientEvents";
        private const string RootName = "AmbientEvents_Active";
        private const int MaxTrackedRejections = 8;

        public readonly struct RejectionRecord
        {
            public readonly float Time;
            public readonly string Reason;

            public RejectionRecord(float time, string reason)
            {
                Time = time;
                Reason = reason;
            }
        }

        [Header("Sources")]
        [SerializeField] private WorldAmbienceController ambienceController;
        [SerializeField] private Transform player;

        [SerializeField]
        [Tooltip("Optional: ground projection through the generated terrain. Without it, positions use the player's height.")]
        private ProceduralTerrainGenerator terrainGenerator;

        [Header("Profiles")]
        [SerializeField] private List<AmbientEventProfile> profiles = new List<AmbientEventProfile>();

        [Header("Cadence")]
        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Seconds between spawn evaluations (never per frame)")]
        private float evaluationInterval = 1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance of one spawn attempt per evaluation when the summed effective weights reach 1")]
        private float baseChancePerEvaluation = 0.35f;

        [SerializeField, Min(0f)]
        [Tooltip("Hard minimum seconds between any two spawns")]
        private float minimumSecondsBetweenSpawns = 2f;

        [SerializeField, Min(1)]
        [Tooltip("Hard cap of simultaneous instances across all profiles")]
        private int globalMaxConcurrent = 6;

        [SerializeField]
        [Tooltip("0 = time-seeded. Any other value makes the director fully deterministic (tests).")]
        private int randomSeed;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos;

        /// <summary>A new instance exists in the world (marker created, registered).</summary>
        public event Action<AmbientEventInstance> OnEventSpawned;

        /// <summary>An instance ended (expired or cleaned up); its marker is being destroyed.</summary>
        public event Action<AmbientEventInstance> OnEventReleased;

        private readonly List<AmbientEventInstance> activeInstances = new List<AmbientEventInstance>();
        private readonly Dictionary<AmbientEventProfile, float> cooldownUntil = new Dictionary<AmbientEventProfile, float>();
        private readonly List<RejectionRecord> recentRejections = new List<RejectionRecord>();

        private System.Random random;
        private Transform rootTransform;
        private float nextEvaluationAt;
        private float lastSpawnTime;
        private bool hasSpawned;
        private bool unusableLogged;
        private float currentEvaluationTime;
        private Func<AmbientEventProfile, bool> eligibilityPredicate;
        private AmbientGroundSampler groundSampler;

        #region Debug API (F7)

        public IReadOnlyList<AmbientEventInstance> ActiveInstances => activeInstances;
        public IReadOnlyList<RejectionRecord> RecentRejections => recentRejections;
        public float NextEvaluationAt => nextEvaluationAt;
        public float LastSpawnTime => lastSpawnTime;
        public bool HasSpawned => hasSpawned;

        public void CollectCooldowns(float now, List<(string eventId, float remaining)> results)
        {
            results.Clear();
            foreach (var pair in cooldownUntil)
            {
                if (pair.Key != null && pair.Value > now)
                {
                    results.Add((pair.Key.EventId, pair.Value - now));
                }
            }
        }

        #endregion

        private void OnEnable()
        {
            random = new System.Random(randomSeed != 0 ? randomSeed : Environment.TickCount);
            eligibilityPredicate = IsEligibleNow;
            groundSampler = SampleGround;
            nextEvaluationAt = 0f;
        }

        private void OnDisable()
        {
            ReleaseAll();
            cooldownUntil.Clear();
            recentRejections.Clear();
            hasSpawned = false;
            unusableLogged = false;
        }

        private void Update()
        {
            float now = Time.time;
            ReleaseExpired(now);

            if (ambienceController == null || player == null)
            {
                if (!unusableLogged)
                {
                    LootboundLog.Warning(LogCategory,
                        "AmbientEventDirector is inactive (missing ambience controller or player)");
                    unusableLogged = true;
                }
                return;
            }

            if (now < nextEvaluationAt)
            {
                return;
            }

            // Missed time is discarded: no catch-up bursts after silence.
            nextEvaluationAt = now + evaluationInterval;
            EvaluateSpawn(now);
        }

        private void EvaluateSpawn(float now)
        {
            if (profiles.Count == 0)
            {
                return;
            }

            if (activeInstances.Count >= globalMaxConcurrent)
            {
                RecordRejection(now, "global concurrency cap reached");
                return;
            }

            if (hasSpawned && now - lastSpawnTime < minimumSecondsBetweenSpawns)
            {
                RecordRejection(now, "global spawn spacing");
                return;
            }

            var state = ambienceController.CurrentState;
            currentEvaluationTime = now;

            if (!AmbientEventSelector.TrySelect(
                    profiles, state, eligibilityPredicate, baseChancePerEvaluation,
                    random, out var profile, out bool hadEligible))
            {
                if (!hadEligible)
                {
                    RecordRejection(now, "no eligible profile (cooldowns, concurrency or zero activity)");
                }
                // A failed chance roll is the normal quiet outcome - not a rejection.
                return;
            }

            if (!AmbientEventPlacement.TryResolvePosition(
                    player.position, player.forward, profile, random, groundSampler, out Vector3 position))
            {
                RecordRejection(now, $"placement failed ({profile.EventId})");
                return;
            }

            Spawn(profile, position, now);
        }

        private void Spawn(AmbientEventProfile profile, Vector3 position, float now)
        {
            Vector2 lifetime = profile.LifetimeRange;
            float duration = Mathf.Lerp(lifetime.x, lifetime.y, (float)random.NextDouble());

            var marker = new GameObject($"AmbientEvent_{profile.EventId}");
            marker.transform.SetParent(EnsureRoot(), worldPositionStays: true);
            marker.transform.position = position;

            var instance = new AmbientEventInstance(profile, position, now, now + duration, marker.transform);
            activeInstances.Add(instance);

            Vector2 cooldown = profile.CooldownRange;
            cooldownUntil[profile] = now + Mathf.Lerp(cooldown.x, cooldown.y, (float)random.NextDouble());

            lastSpawnTime = now;
            hasSpawned = true;
            OnEventSpawned?.Invoke(instance);
        }

        private void ReleaseExpired(float now)
        {
            for (int i = activeInstances.Count - 1; i >= 0; i--)
            {
                if (now >= activeInstances[i].ExpirationTime)
                {
                    ReleaseAt(i);
                }
            }
        }

        private void ReleaseAll()
        {
            for (int i = activeInstances.Count - 1; i >= 0; i--)
            {
                ReleaseAt(i);
            }

            if (rootTransform != null)
            {
                Destroy(rootTransform.gameObject);
                rootTransform = null;
            }
        }

        private void ReleaseAt(int index)
        {
            var instance = activeInstances[index];
            activeInstances.RemoveAt(index);

            if (instance.MarkerTransform != null)
            {
                Destroy(instance.MarkerTransform.gameObject);
            }

            OnEventReleased?.Invoke(instance);
        }

        private bool IsEligibleNow(AmbientEventProfile profile)
        {
            if (cooldownUntil.TryGetValue(profile, out float until) && currentEvaluationTime < until)
            {
                return false;
            }

            return CountActive(profile) < profile.MaxConcurrent;
        }

        private int CountActive(AmbientEventProfile profile)
        {
            int count = 0;
            for (int i = 0; i < activeInstances.Count; i++)
            {
                if (activeInstances[i].Profile == profile)
                {
                    count++;
                }
            }

            return count;
        }

        private bool SampleGround(float worldX, float worldZ, out float worldY)
        {
            worldY = 0f;
            var context = terrainGenerator != null ? terrainGenerator.Context : null;
            if (context == null || context.NormalizedHeightMap == null)
            {
                return false;
            }

            worldY = context.SampleHeightAtWorld(worldX, worldZ);
            return true;
        }

        private Transform EnsureRoot()
        {
            if (rootTransform == null)
            {
                var root = new GameObject(RootName);
                root.transform.SetParent(transform, false);
                rootTransform = root.transform;
            }

            return rootTransform;
        }

        private void RecordRejection(float now, string reason)
        {
            recentRejections.Add(new RejectionRecord(now, reason));
            if (recentRejections.Count > MaxTrackedRejections)
            {
                recentRejections.RemoveAt(0);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos || !Application.isPlaying || player == null)
            {
                return;
            }

            // Union of the authored distance rings around the player.
            float minRadius = float.MaxValue;
            float maxRadius = 0f;
            foreach (var profile in profiles)
            {
                if (profile == null) continue;
                Vector2 range = profile.DistanceRange;
                minRadius = Mathf.Min(minRadius, range.x);
                maxRadius = Mathf.Max(maxRadius, range.y);
            }

            if (maxRadius > 0f)
            {
                UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.25f);
                UnityEditor.Handles.DrawWireDisc(player.position, Vector3.up, minRadius);
                UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.45f);
                UnityEditor.Handles.DrawWireDisc(player.position, Vector3.up, maxRadius);
            }

            float now = Application.isPlaying ? Time.time : 0f;
            foreach (var instance in activeInstances)
            {
                Color color = CategoryColor(instance.Profile != null ? instance.Profile.Category : AmbientEventCategory.Rare);
                Gizmos.color = color;
                Gizmos.DrawWireSphere(instance.Position, 0.75f);
                Gizmos.DrawLine(instance.Position, player.position);
                UnityEditor.Handles.Label(
                    instance.Position + Vector3.up,
                    $"{instance.Profile?.EventId} ({instance.Profile?.Category})  {instance.RemainingSeconds(now):F1}s");
            }
        }

        private static Color CategoryColor(AmbientEventCategory category)
        {
            switch (category)
            {
                case AmbientEventCategory.Birds: return Color.cyan;
                case AmbientEventCategory.Insects: return Color.green;
                case AmbientEventCategory.Wind: return new Color(0.8f, 0.8f, 0.8f);
                case AmbientEventCategory.Environmental: return Color.yellow;
                case AmbientEventCategory.Rare: return Color.magenta;
                default: return Color.white;
            }
        }
#endif
    }
}
