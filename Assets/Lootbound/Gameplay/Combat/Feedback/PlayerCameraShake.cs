using UnityEngine;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Simple camera shake component.
    /// Attach to the camera GameObject.
    /// </summary>
    public class PlayerCameraShake : MonoBehaviour
    {
        private Vector3 originalLocalPosition;
        private float shakeTimer;
        private float shakeIntensity;
        private bool isShaking;

        private void Awake()
        {
            originalLocalPosition = transform.localPosition;
        }

        private void LateUpdate()
        {
            if (!isShaking)
            {
                return;
            }

            shakeTimer -= Time.deltaTime;

            if (shakeTimer <= 0f)
            {
                isShaking = false;
                transform.localPosition = originalLocalPosition;
                return;
            }

            // Apply random offset
            Vector3 offset = new Vector3(
                Random.Range(-1f, 1f) * shakeIntensity,
                Random.Range(-1f, 1f) * shakeIntensity,
                0f
            );

            transform.localPosition = originalLocalPosition + offset;
        }

        /// <summary>
        /// Start a camera shake.
        /// </summary>
        /// <param name="intensity">Shake intensity (maximum offset).</param>
        /// <param name="duration">Shake duration in seconds.</param>
        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f)
            {
                return;
            }

            // Take the stronger shake if one is already in progress
            if (isShaking && intensity < shakeIntensity)
            {
                return;
            }

            shakeIntensity = intensity;
            shakeTimer = duration;
            isShaking = true;
        }

        /// <summary>
        /// Stop any active shake immediately.
        /// </summary>
        public void StopShake()
        {
            isShaking = false;
            transform.localPosition = originalLocalPosition;
        }
    }
}
