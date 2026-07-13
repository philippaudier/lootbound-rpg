using System;
using System.Collections.Generic;
using UnityEngine;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Handles player melee weapon attack phases and hit detection.
    /// </summary>
    public class PlayerMeleeWeapon : MonoBehaviour
    {
        private const string Category = "PlayerMeleeWeapon";

        [Header("Configuration")]
        [SerializeField] private MeleeAttackConfig attackConfig;

        [Header("Detection")]
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private LayerMask targetLayers;
        [SerializeField] private LayerMask obstacleLayers;

        [Header("Debug")]
        [SerializeField] private bool debugDraw = false;

        private AttackPhase currentPhase = AttackPhase.Ready;
        private float attackTimer;
        private MeleeHitDetector hitDetector;

        /// <summary>
        /// Current attack phase.
        /// </summary>
        public AttackPhase CurrentPhase => currentPhase;

        /// <summary>
        /// True if currently attacking (not in Ready phase).
        /// </summary>
        public bool IsAttacking => currentPhase != AttackPhase.Ready;

        /// <summary>
        /// True if able to start a new attack.
        /// </summary>
        public bool CanAttack => currentPhase == AttackPhase.Ready;

        /// <summary>
        /// Progress through the current attack (0-1).
        /// </summary>
        public float AttackProgress => attackConfig != null && attackConfig.TotalDuration > 0
            ? Mathf.Clamp01(attackTimer / attackConfig.TotalDuration)
            : 0f;

        /// <summary>
        /// Number of targets hit during the current attack.
        /// </summary>
        public int HitsThisAttack => hitDetector?.HitCount ?? 0;

        /// <summary>
        /// Fired when an attack starts.
        /// </summary>
        public event Action OnAttackStarted;

        /// <summary>
        /// Fired when the active window begins.
        /// </summary>
        public event Action OnActiveWindowStarted;

        /// <summary>
        /// Fired when a target is hit. Parameter: DamageResult.
        /// </summary>
        public event Action<DamageResult> OnHit;

        /// <summary>
        /// Fired when an attack ends.
        /// </summary>
        public event Action OnAttackEnded;

        private void Awake()
        {
            hitDetector = gameObject.AddComponent<MeleeHitDetector>();
            hitDetector.SetLayers(targetLayers, obstacleLayers);

            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }
        }

        private void Update()
        {
            if (!IsAttacking)
            {
                return;
            }

            UpdateAttack();
        }

        /// <summary>
        /// Start a new attack if possible.
        /// </summary>
        /// <returns>True if attack started.</returns>
        public bool TryAttack()
        {
            if (!CanAttack || attackConfig == null)
            {
                return false;
            }

            StartAttack();
            return true;
        }

        private void StartAttack()
        {
            currentPhase = AttackPhase.Windup;
            attackTimer = 0f;
            hitDetector.ResetForNewAttack();

            LootboundLog.Info(Category, "Attack started - Windup phase");
            OnAttackStarted?.Invoke();
        }

        private void UpdateAttack()
        {
            attackTimer += Time.deltaTime;

            switch (currentPhase)
            {
                case AttackPhase.Windup:
                    if (attackTimer >= attackConfig.ActiveWindowStart)
                    {
                        EnterActivePhase();
                    }
                    break;

                case AttackPhase.Active:
                    PerformHitDetection();

                    if (attackTimer >= attackConfig.ActiveWindowEnd)
                    {
                        EnterRecoveryPhase();
                    }
                    break;

                case AttackPhase.Recovery:
                    if (attackTimer >= attackConfig.TotalDuration)
                    {
                        EndAttack();
                    }
                    break;
            }
        }

        private void EnterActivePhase()
        {
            currentPhase = AttackPhase.Active;
            LootboundLog.Info(Category, "Attack - Active phase (can hit)");
            OnActiveWindowStarted?.Invoke();
        }

        private void EnterRecoveryPhase()
        {
            currentPhase = AttackPhase.Recovery;
            LootboundLog.Info(Category, $"Attack - Recovery phase. Hits this attack: {hitDetector.HitCount}");
        }

        private void EndAttack()
        {
            currentPhase = AttackPhase.Ready;
            LootboundLog.Info(Category, "Attack ended");
            OnAttackEnded?.Invoke();
        }

        private void PerformHitDetection()
        {
            if (attackConfig == null)
            {
                return;
            }

            Vector3 origin = attackOrigin.position;
            Vector3 direction = attackOrigin.forward;

            var results = hitDetector.DetectAndDamage(
                origin,
                direction,
                attackConfig.TraceRadius,
                attackConfig.Range,
                gameObject,
                attackConfig.Damage,
                attackConfig.StaggerForce
            );

            foreach (var result in results)
            {
                OnHit?.Invoke(result);
            }

            if (debugDraw)
            {
                // Draw attack sphere cast
                Debug.DrawRay(origin, direction * attackConfig.Range, Color.yellow, 0.1f);
            }
        }

        /// <summary>
        /// Force interrupt the current attack.
        /// </summary>
        public void InterruptAttack()
        {
            if (IsAttacking)
            {
                currentPhase = AttackPhase.Ready;
                OnAttackEnded?.Invoke();
            }
        }

        /// <summary>
        /// Set the attack configuration at runtime.
        /// </summary>
        public void SetConfig(MeleeAttackConfig config)
        {
            attackConfig = config;
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDraw || attackConfig == null || attackOrigin == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackOrigin.position, attackConfig.TraceRadius);
            Gizmos.DrawRay(attackOrigin.position, attackOrigin.forward * attackConfig.Range);
        }
    }
}
