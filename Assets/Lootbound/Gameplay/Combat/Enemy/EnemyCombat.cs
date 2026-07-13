using System;
using UnityEngine;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Handles enemy attack hit detection.
    /// Works with EnemyBrain to detect hits during AttackActive state.
    /// </summary>
    public class EnemyCombat : MonoBehaviour
    {
        private const string Category = "EnemyCombat";

        [Header("Configuration")]
        [SerializeField] private EnemyConfig config;

        [Header("Attack Detection")]
        [Tooltip("Origin point for attack detection.")]
        [SerializeField] private Transform attackOrigin;

        [Tooltip("Layers that can be hit by this enemy.")]
        [SerializeField] private LayerMask targetLayers;

        [Tooltip("Radius of the attack sphere cast.")]
        [SerializeField] private float attackRadius = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool debugDraw = false;

        private EnemyBrain brain;
        private bool hasHitThisAttack;
        private RaycastHit[] hitBuffer = new RaycastHit[5];

        /// <summary>
        /// Fired when the enemy hits a target.
        /// </summary>
        public event Action<DamageResult> OnHit;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();

            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }
        }

        private void OnEnable()
        {
            if (brain != null)
            {
                brain.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (brain != null)
            {
                brain.OnStateChanged -= HandleStateChanged;
            }
        }

        private void Update()
        {
            if (brain == null || brain.CurrentState != EnemyState.AttackActive)
            {
                return;
            }

            // Only hit once per attack
            if (!hasHitThisAttack)
            {
                PerformAttackDetection();
            }
        }

        private void HandleStateChanged(EnemyState oldState, EnemyState newState)
        {
            // Reset hit tracking when starting a new attack
            if (newState == EnemyState.AttackWindup)
            {
                hasHitThisAttack = false;
            }
        }

        private void PerformAttackDetection()
        {
            if (config == null)
            {
                return;
            }

            Vector3 origin = attackOrigin.position;
            Vector3 direction = transform.forward;
            float range = config.AttackRange;

            int hitCount = Physics.SphereCastNonAlloc(origin, attackRadius, direction, hitBuffer, range, targetLayers);

            if (debugDraw)
            {
                Debug.DrawRay(origin, direction * range, hitCount > 0 ? Color.green : Color.red, 0.1f);
            }

            for (int i = 0; i < hitCount; i++)
            {
                var hit = hitBuffer[i];

                // Skip self
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform)
                {
                    continue;
                }

                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    var request = new DamageRequest(
                        gameObject,
                        config.AttackDamage,
                        hit.point,
                        direction,
                        config.AttackStaggerForce
                    );

                    var result = damageable.TakeDamage(request);

                    if (result.Applied)
                    {
                        hasHitThisAttack = true;
                        LootboundLog.Info(Category, $"{gameObject.name} hit {hit.collider.name} for {result.DamageDealt} damage");
                        OnHit?.Invoke(result);
                        break; // Only one hit per attack
                    }
                    else if (result.WasBlocked)
                    {
                        hasHitThisAttack = true;
                        LootboundLog.Info(Category, $"{gameObject.name} attack was blocked by {hit.collider.name}");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Set configuration at runtime.
        /// </summary>
        public void SetConfig(EnemyConfig newConfig)
        {
            config = newConfig;
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDraw || attackOrigin == null)
            {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackOrigin.position, attackRadius);

            if (config != null)
            {
                Gizmos.DrawRay(attackOrigin.position, transform.forward * config.AttackRange);
            }
        }
    }
}
