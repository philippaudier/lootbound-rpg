using System.Collections;
using UnityEngine;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Handles visual feedback for enemies: hit flash and death effects.
    /// </summary>
    public class EnemyVisuals : MonoBehaviour
    {
        private const string Category = "EnemyVisuals";

        [Header("Hit Feedback")]
        [Tooltip("Color to flash when hit.")]
        [SerializeField] private Color hitColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Tooltip("Duration of the hit flash.")]
        [SerializeField] private float hitFlashDuration = 0.15f;

        [Header("Death Feedback")]
        [Tooltip("Duration of the death effect before destruction.")]
        [SerializeField] private float deathDuration = 1f;

        [Tooltip("How fast to sink into the ground on death.")]
        [SerializeField] private float sinkSpeed = 0.5f;

        [Tooltip("Delay before starting to sink.")]
        [SerializeField] private float sinkDelay = 0.3f;

        [Header("References")]
        [Tooltip("Renderers to flash. If empty, will auto-find on this object and children.")]
        [SerializeField] private Renderer[] renderers;

        private EnemyHealth health;
        private MaterialPropertyBlock propertyBlock;
        private Color[] originalColors;
        private Coroutine hitFlashCoroutine;
        private bool isDead;

        // Shader property IDs
        private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            propertyBlock = new MaterialPropertyBlock();

            // Auto-find renderers if not assigned
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>();
            }

            // Store original colors
            CacheOriginalColors();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDamaged += HandleDamaged;
                health.OnDied += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDamaged -= HandleDamaged;
                health.OnDied -= HandleDied;
            }
        }

        private void CacheOriginalColors()
        {
            if (renderers == null || renderers.Length == 0) return;

            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sharedMaterial != null)
                {
                    // Try to get the base color
                    if (renderers[i].sharedMaterial.HasProperty(ColorProperty))
                    {
                        originalColors[i] = renderers[i].sharedMaterial.GetColor(ColorProperty);
                    }
                    else
                    {
                        originalColors[i] = Color.white;
                    }
                }
            }
        }

        private void HandleDamaged(DamageRequest request)
        {
            if (isDead) return;

            // Start hit flash
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
            }
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private void HandleDied()
        {
            if (isDead) return;

            isDead = true;
            LootboundLog.Info(Category, $"{gameObject.name} - Starting death visuals");

            // Stop any ongoing hit flash
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }

            // Start death effect
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            // Flash to hit color
            SetColor(hitColor);

            yield return new WaitForSeconds(hitFlashDuration);

            // Return to original color
            RestoreOriginalColors();
            hitFlashCoroutine = null;
        }

        private IEnumerator DeathRoutine()
        {
            // Brief pause before starting to sink
            yield return new WaitForSeconds(sinkDelay);

            // Disable collider so player can't interact
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            // Start sinking and fading
            float elapsed = 0f;
            Vector3 startPosition = transform.position;
            float sinkDistance = sinkSpeed * (deathDuration - sinkDelay);

            while (elapsed < deathDuration - sinkDelay)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (deathDuration - sinkDelay);

                // Sink into ground
                transform.position = startPosition - Vector3.up * (sinkDistance * t);

                // Fade out (darken color)
                float fadeT = Mathf.SmoothStep(0f, 1f, t);
                SetColorFade(1f - fadeT);

                yield return null;
            }

            // Destroy the enemy
            LootboundLog.Info(Category, $"{gameObject.name} - Death complete, destroying");
            Destroy(gameObject);
        }

        private void SetColor(Color color)
        {
            if (renderers == null) return;

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorProperty, color);

                // Also set emission for a stronger flash effect
                propertyBlock.SetColor(EmissionColorProperty, color * 0.3f);

                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void SetColorFade(float alpha)
        {
            if (renderers == null || originalColors == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                renderers[i].GetPropertyBlock(propertyBlock);

                // Darken toward black as we fade
                Color fadedColor = Color.Lerp(Color.black, originalColors[i], alpha);
                propertyBlock.SetColor(ColorProperty, fadedColor);

                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }

        private void RestoreOriginalColors()
        {
            if (renderers == null || originalColors == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                renderers[i].GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorProperty, originalColors[i]);
                propertyBlock.SetColor(EmissionColorProperty, Color.black);
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }

        /// <summary>
        /// Manually trigger a hit flash (for testing or special effects).
        /// </summary>
        public void TriggerHitFlash()
        {
            if (isDead) return;

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
            }
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
            deathDuration = Mathf.Max(0.1f, deathDuration);
            sinkSpeed = Mathf.Max(0f, sinkSpeed);
            sinkDelay = Mathf.Clamp(sinkDelay, 0f, deathDuration - 0.1f);
        }
#endif
    }
}
