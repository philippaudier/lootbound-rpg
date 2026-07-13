using System.Collections;
using UnityEngine;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Handles combat feedback effects: hitstop, camera shake, and damage flash.
    /// </summary>
    public class CombatFeedback : MonoBehaviour
    {
        private const string Category = "CombatFeedback";

        [Header("Hitstop")]
        [Tooltip("Default hitstop duration in seconds.")]
        [SerializeField] private float defaultHitstopDuration = 0.05f;

        [Tooltip("Time scale during hitstop (0 = frozen, 0.1 = slow motion).")]
        [SerializeField] private float hitstopTimeScale = 0.0f;

        [Header("Camera Shake")]
        [Tooltip("Default camera shake intensity.")]
        [SerializeField] private float defaultShakeIntensity = 0.15f;

        [Tooltip("Default camera shake duration.")]
        [SerializeField] private float defaultShakeDuration = 0.1f;

        [Header("Dependencies")]
        [SerializeField] private PlayerCameraShake cameraShake;

        private Coroutine hitstopCoroutine;
        private float originalTimeScale = 1f;

        private void Awake()
        {
            if (cameraShake == null)
            {
                cameraShake = FindFirstObjectByType<PlayerCameraShake>();
            }
        }

        /// <summary>
        /// Play a hit effect with all feedback elements.
        /// </summary>
        /// <param name="hitPosition">World position of the hit.</param>
        /// <param name="hitDirection">Direction of the hit.</param>
        /// <param name="hitstopDuration">Optional override for hitstop duration.</param>
        public void PlayHitEffect(Vector3 hitPosition, Vector3 hitDirection, float? hitstopDuration = null)
        {
            float duration = hitstopDuration ?? defaultHitstopDuration;
            ApplyHitstop(duration);
            ShakeCamera(defaultShakeIntensity, defaultShakeDuration);

            LootboundLog.Info(Category, $"Hit effect at {hitPosition}");
        }

        /// <summary>
        /// Apply hitstop effect (brief time freeze).
        /// </summary>
        /// <param name="duration">Duration in seconds.</param>
        public void ApplyHitstop(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            if (hitstopCoroutine != null)
            {
                StopCoroutine(hitstopCoroutine);
                Time.timeScale = originalTimeScale;
            }

            hitstopCoroutine = StartCoroutine(HitstopCoroutine(duration));
        }

        private IEnumerator HitstopCoroutine(float duration)
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = hitstopTimeScale;

            // Use unscaled time so hitstop actually works
            yield return new WaitForSecondsRealtime(duration);

            Time.timeScale = originalTimeScale;
            hitstopCoroutine = null;
        }

        /// <summary>
        /// Shake the camera.
        /// </summary>
        /// <param name="intensity">Shake intensity (default if omitted).</param>
        /// <param name="duration">Shake duration (default if omitted).</param>
        public void ShakeCamera(float? intensity = null, float? duration = null)
        {
            if (cameraShake != null)
            {
                cameraShake.Shake(
                    intensity ?? defaultShakeIntensity,
                    duration ?? defaultShakeDuration
                );
            }
        }

        /// <summary>
        /// Play damage received flash effect.
        /// </summary>
        public void PlayDamageFlash()
        {
            // This will be implemented by CombatHUDController
            // Could emit an event here that UI listens to
            LootboundLog.Info(Category, "Damage flash triggered");
        }

        private void OnDestroy()
        {
            // Ensure time scale is restored if we're destroyed during hitstop
            if (hitstopCoroutine != null)
            {
                Time.timeScale = originalTimeScale;
            }
        }
    }
}
