using System.Collections.Generic;
using UnityEngine;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Handles hit detection for melee attacks using SphereCast.
    /// Tracks hit targets to prevent double-hits per attack.
    /// </summary>
    public class MeleeHitDetector : MonoBehaviour
    {
        private const string Category = "MeleeHitDetector";

        [Header("Detection Settings")]
        [Tooltip("Layer mask for valid hit targets.")]
        [SerializeField] private LayerMask hitLayers;

        [Tooltip("Layer mask for obstacles that block attacks.")]
        [SerializeField] private LayerMask obstacleLayers;

        [Tooltip("Maximum number of hits to detect per frame.")]
        [SerializeField] private int maxHitsPerFrame = 10;

        [Header("Debug")]
        [SerializeField] private bool debugDrawRays = false;

        private readonly HashSet<Collider> hitTargetsThisAttack = new();
        private RaycastHit[] hitBuffer;

        /// <summary>
        /// Number of unique targets hit during the current attack.
        /// </summary>
        public int HitCount => hitTargetsThisAttack.Count;

        /// <summary>
        /// Read-only view of hit targets this attack.
        /// </summary>
        public IReadOnlyCollection<Collider> HitTargets => hitTargetsThisAttack;

        private void Awake()
        {
            hitBuffer = new RaycastHit[maxHitsPerFrame];
        }

        /// <summary>
        /// Reset hit tracking for a new attack.
        /// </summary>
        public void ResetForNewAttack()
        {
            hitTargetsThisAttack.Clear();
        }

        /// <summary>
        /// Configure layer masks at runtime.
        /// </summary>
        /// <param name="targetLayers">Layers that can be hit.</param>
        /// <param name="blockerLayers">Layers that block attacks (walls, etc).</param>
        public void SetLayers(LayerMask targetLayers, LayerMask blockerLayers)
        {
            hitLayers = targetLayers;
            obstacleLayers = blockerLayers;
        }

        /// <summary>
        /// Check if there's a clear line of sight to the target (no obstacles blocking).
        /// </summary>
        private bool HasLineOfSight(Vector3 origin, Vector3 targetPoint)
        {
            if (obstacleLayers == 0)
            {
                return true; // No obstacle layers configured
            }

            Vector3 direction = targetPoint - origin;
            float distance = direction.magnitude;

            if (distance < 0.01f)
            {
                return true;
            }

            // Check if any obstacle blocks the path
            return !Physics.Raycast(origin, direction.normalized, distance, obstacleLayers);
        }

        /// <summary>
        /// Perform a sphere cast and return all newly hit damageable targets.
        /// </summary>
        /// <param name="origin">Start point of the cast.</param>
        /// <param name="direction">Direction of the cast.</param>
        /// <param name="radius">Radius of the sphere.</param>
        /// <param name="maxDistance">Maximum distance to check.</param>
        /// <returns>List of newly hit damageable components.</returns>
        public List<IDamageable> DetectHits(Vector3 origin, Vector3 direction, float radius, float maxDistance)
        {
            var newHits = new List<IDamageable>();

            int hitCount = Physics.SphereCastNonAlloc(origin, radius, direction, hitBuffer, maxDistance, hitLayers);

            if (debugDrawRays)
            {
                Debug.DrawRay(origin, direction * maxDistance, hitCount > 0 ? Color.green : Color.red, 0.1f);
            }

            for (int i = 0; i < hitCount; i++)
            {
                var hit = hitBuffer[i];

                // Skip if already hit this attack
                if (hitTargetsThisAttack.Contains(hit.collider))
                {
                    continue;
                }

                // Check wall blocking - use hit.point if valid, otherwise use collider center
                Vector3 hitPoint = hit.point != Vector3.zero ? hit.point : hit.collider.bounds.center;
                if (!HasLineOfSight(origin, hitPoint))
                {
                    if (debugDrawRays)
                    {
                        Debug.DrawLine(origin, hitPoint, Color.gray, 0.1f);
                    }
                    continue;
                }

                // Look for IDamageable on the hit object or its parent
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    hitTargetsThisAttack.Add(hit.collider);
                    newHits.Add(damageable);

                    LootboundLog.Info(Category, $"Hit target: {hit.collider.name}");
                }
            }

            return newHits;
        }

        /// <summary>
        /// Perform detection and immediately apply damage to all hit targets.
        /// </summary>
        /// <param name="origin">Start point of the cast.</param>
        /// <param name="direction">Direction of the cast.</param>
        /// <param name="radius">Radius of the sphere.</param>
        /// <param name="maxDistance">Maximum distance to check.</param>
        /// <param name="damageSource">Source GameObject for damage attribution.</param>
        /// <param name="damage">Amount of damage to deal.</param>
        /// <param name="staggerForce">Stagger force to apply.</param>
        /// <returns>List of damage results for all hits.</returns>
        public List<DamageResult> DetectAndDamage(
            Vector3 origin,
            Vector3 direction,
            float radius,
            float maxDistance,
            GameObject damageSource,
            float damage,
            float staggerForce)
        {
            var results = new List<DamageResult>();
            int hitCount = Physics.SphereCastNonAlloc(origin, radius, direction, hitBuffer, maxDistance, hitLayers);

            if (debugDrawRays)
            {
                Debug.DrawRay(origin, direction * maxDistance, hitCount > 0 ? Color.green : Color.red, 0.1f);
            }

            for (int i = 0; i < hitCount; i++)
            {
                var hit = hitBuffer[i];

                // Skip if already hit this attack
                if (hitTargetsThisAttack.Contains(hit.collider))
                {
                    continue;
                }

                // Check wall blocking - use hit.point if valid, otherwise use collider center
                Vector3 hitPoint = hit.point != Vector3.zero ? hit.point : hit.collider.bounds.center;
                if (!HasLineOfSight(origin, hitPoint))
                {
                    if (debugDrawRays)
                    {
                        Debug.DrawLine(origin, hitPoint, Color.gray, 0.1f);
                    }
                    continue;
                }

                // Look for IDamageable on the hit object or its parent
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    hitTargetsThisAttack.Add(hit.collider);

                    var request = new DamageRequest(
                        damageSource,
                        damage,
                        hitPoint,
                        direction,
                        staggerForce
                    );

                    var result = damageable.TakeDamage(request);
                    results.Add(result);

                    LootboundLog.Info(Category, $"Hit {hit.collider.name} for {result.DamageDealt} damage (fatal: {result.WasFatal})");
                }
            }

            return results;
        }

        private void OnValidate()
        {
            maxHitsPerFrame = Mathf.Max(1, maxHitsPerFrame);
        }
    }
}
