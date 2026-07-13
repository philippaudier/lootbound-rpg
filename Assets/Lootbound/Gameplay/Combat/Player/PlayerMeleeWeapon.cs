using System;
using System.Collections.Generic;
using UnityEngine;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.Equipment;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Handles player melee weapon attack phases and hit detection.
    /// Uses equipment stats when available, falls back to config values.
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
        private ResolvedWeaponStats equipmentStats;
        private bool hasEquipmentStats;

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
        public float AttackProgress
        {
            get
            {
                if (attackConfig == null || attackConfig.TotalDuration <= 0) return 0f;
                float totalDuration = attackConfig.TotalDuration * GetDurationMultiplier();
                return Mathf.Clamp01(attackTimer / totalDuration);
            }
        }

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

            // Apply duration multiplier from equipment stats (lower = faster attacks)
            float durationMult = GetDurationMultiplier();

            switch (currentPhase)
            {
                case AttackPhase.Windup:
                    if (attackTimer >= attackConfig.ActiveWindowStart * durationMult)
                    {
                        EnterActivePhase();
                    }
                    break;

                case AttackPhase.Active:
                    PerformHitDetection();

                    if (attackTimer >= attackConfig.ActiveWindowEnd * durationMult)
                    {
                        EnterRecoveryPhase();
                    }
                    break;

                case AttackPhase.Recovery:
                    if (attackTimer >= attackConfig.TotalDuration * durationMult)
                    {
                        EndAttack();
                    }
                    break;
            }
        }

        /// <summary>
        /// Get duration multiplier for attack phases.
        /// Lower value = faster attacks.
        /// </summary>
        private float GetDurationMultiplier()
        {
            if (hasEquipmentStats && equipmentStats.DurationMultiplier > 0)
            {
                // Clamp to prevent absurdly fast or slow attacks
                return Mathf.Clamp(equipmentStats.DurationMultiplier, 0.5f, 2f);
            }
            return 1f;
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

            // Use equipment stats if available, otherwise fall back to config
            float damage = GetEffectiveDamage();
            float range = GetEffectiveRange();
            float stagger = GetEffectiveStagger();

            var results = hitDetector.DetectAndDamage(
                origin,
                direction,
                attackConfig.TraceRadius,
                range,
                gameObject,
                damage,
                stagger
            );

            foreach (var result in results)
            {
                OnHit?.Invoke(result);
            }

            if (debugDraw)
            {
                // Draw attack sphere cast
                Debug.DrawRay(origin, direction * range, Color.yellow, 0.1f);
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

        /// <summary>
        /// Set equipment stats to use for damage/range calculations.
        /// </summary>
        public void SetEquipmentStats(ResolvedWeaponStats stats)
        {
            equipmentStats = stats;
            hasEquipmentStats = stats.IsValid;

            if (hasEquipmentStats)
            {
                LootboundLog.Info(Category, $"Equipment stats applied: {stats}");
            }
        }

        /// <summary>
        /// Clear equipment stats, reverting to config values.
        /// </summary>
        public void ClearEquipmentStats()
        {
            hasEquipmentStats = false;
            LootboundLog.Info(Category, "Equipment stats cleared");
        }

        /// <summary>
        /// Get effective damage for current attack.
        /// </summary>
        public float GetEffectiveDamage()
        {
            if (hasEquipmentStats) return equipmentStats.Damage;
            return attackConfig?.Damage ?? 20f;
        }

        /// <summary>
        /// Get effective range for current attack.
        /// </summary>
        public float GetEffectiveRange()
        {
            if (hasEquipmentStats) return equipmentStats.Range;
            return attackConfig?.Range ?? 2f;
        }

        /// <summary>
        /// Get effective stagger for current attack.
        /// </summary>
        public float GetEffectiveStagger()
        {
            if (hasEquipmentStats) return equipmentStats.Stagger;
            return attackConfig?.StaggerForce ?? 0.3f;
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDraw || attackConfig == null || attackOrigin == null)
            {
                return;
            }

            float range = GetEffectiveRange();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackOrigin.position, attackConfig.TraceRadius);
            Gizmos.DrawRay(attackOrigin.position, attackOrigin.forward * range);
        }
    }
}
