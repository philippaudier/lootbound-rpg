using UnityEngine;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Immutable request representing incoming damage.
    /// </summary>
    public readonly struct DamageRequest
    {
        /// <summary>
        /// The GameObject that caused this damage.
        /// </summary>
        public readonly GameObject Source;

        /// <summary>
        /// Amount of damage to apply.
        /// </summary>
        public readonly float Amount;

        /// <summary>
        /// World position where the hit occurred.
        /// </summary>
        public readonly Vector3 HitPoint;

        /// <summary>
        /// Direction the damage came from (normalized).
        /// </summary>
        public readonly Vector3 HitDirection;

        /// <summary>
        /// Force applied for stagger effects (0-1 normalized).
        /// </summary>
        public readonly float StaggerForce;

        /// <summary>
        /// Validates that this damage request has valid values.
        /// </summary>
        public bool IsValid => Amount > 0f;

        public DamageRequest(GameObject source, float amount, Vector3 hitPoint, Vector3 hitDirection, float staggerForce = 0f)
        {
            Source = source;
            Amount = amount;
            HitPoint = hitPoint;
            HitDirection = hitDirection.sqrMagnitude > 0f ? hitDirection.normalized : Vector3.forward;
            StaggerForce = Mathf.Clamp01(staggerForce);
        }
    }
}
