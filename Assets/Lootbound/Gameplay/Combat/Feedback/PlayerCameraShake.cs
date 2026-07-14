using UnityEngine;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Simple camera shake component.
    /// Attach to the camera GameObject.
    /// Uses offset-based approach to avoid conflicts with other systems that move the camera.
    /// </summary>
    public class PlayerCameraShake : MonoBehaviour
    {
        private float shakeTimer;
        private float shakeIntensity;
        private float shakeDuration;
        private bool isShaking;
        private Vector3 currentShakeOffset;

        private void LateUpdate()
        {
            if (!isShaking)
            {
                return;
            }

            // Remove previous frame's offset first
            transform.localPosition -= currentShakeOffset;
            transform.localRotation *= Quaternion.Inverse(Quaternion.Euler(currentShakeOffset.y * 2f, currentShakeOffset.x * 2f, 0f));

            shakeTimer -= Time.deltaTime;

            if (shakeTimer <= 0f)
            {
                isShaking = false;
                currentShakeOffset = Vector3.zero;
                return;
            }

            // Calculate intensity falloff (stronger at start, weaker at end)
            float progress = shakeTimer / shakeDuration;
            float currentIntensity = shakeIntensity * progress;

            // Generate random offset
            currentShakeOffset = new Vector3(
                Random.Range(-1f, 1f) * currentIntensity,
                Random.Range(-1f, 1f) * currentIntensity,
                0f
            );

            // Apply new offset
            transform.localPosition += currentShakeOffset;

            // Also apply slight rotation for more impact
            transform.localRotation *= Quaternion.Euler(currentShakeOffset.y * 2f, currentShakeOffset.x * 2f, 0f);
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
            shakeDuration = duration;
            isShaking = true;
        }

        /// <summary>
        /// Stop any active shake immediately.
        /// </summary>
        public void StopShake()
        {
            if (isShaking)
            {
                // Remove current offset
                transform.localPosition -= currentShakeOffset;
                transform.localRotation *= Quaternion.Inverse(Quaternion.Euler(currentShakeOffset.y * 2f, currentShakeOffset.x * 2f, 0f));
                currentShakeOffset = Vector3.zero;
            }
            isShaking = false;
        }
    }
}
