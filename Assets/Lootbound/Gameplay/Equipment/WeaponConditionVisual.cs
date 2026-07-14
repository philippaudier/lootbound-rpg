using UnityEngine;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Applies visual feedback to the first-person weapon based on equipment condition.
    /// Uses MaterialPropertyBlock to avoid material instantiation.
    /// </summary>
    public class WeaponConditionVisual : MonoBehaviour
    {
        private const string Category = "WeaponConditionVisual";

        [Header("Dependencies")]
        [SerializeField] private PlayerEquipment playerEquipment;
        [SerializeField] private PlayerWeaponWear playerWeaponWear;

        [Header("Broken Effect")]
        [Tooltip("Desaturation amount when broken (0 = normal, 1 = grayscale)")]
        [SerializeField, Range(0f, 1f)]
        private float brokenDesaturation = 0.6f;

        [Tooltip("Tint color when broken")]
        [SerializeField]
        private Color brokenTint = new Color(0.8f, 0.3f, 0.3f, 1f);

        [Tooltip("Tint strength when broken")]
        [SerializeField, Range(0f, 1f)]
        private float brokenTintStrength = 0.3f;

        private MaterialPropertyBlock propertyBlock;
        private Renderer[] weaponRenderers;
        private bool isEffectApplied;

        // Shader property IDs
        private static readonly int ColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int TintPropertyId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();

            // Auto-find dependencies
            if (playerEquipment == null)
            {
                playerEquipment = GetComponentInParent<PlayerEquipment>();
            }

            if (playerWeaponWear == null)
            {
                playerWeaponWear = GetComponentInParent<PlayerWeaponWear>();
            }
        }

        private void OnEnable()
        {
            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnConditionChanged += HandleConditionChanged;
            }

            if (playerEquipment != null)
            {
                playerEquipment.OnWeaponEquipped += HandleWeaponEquipped;
                playerEquipment.OnWeaponUnequipped += HandleWeaponUnequipped;
            }
        }

        private void OnDisable()
        {
            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnConditionChanged -= HandleConditionChanged;
            }

            if (playerEquipment != null)
            {
                playerEquipment.OnWeaponEquipped -= HandleWeaponEquipped;
                playerEquipment.OnWeaponUnequipped -= HandleWeaponUnequipped;
            }
        }

        private void HandleConditionChanged(WearResult result)
        {
            if (result.NowBroken)
            {
                ApplyBrokenEffect();
            }
        }

        private void HandleWeaponEquipped(Inventory.ItemInstance item)
        {
            // Wait one frame for weapon view to be instantiated
            StartCoroutine(UpdateVisualsNextFrame());
        }

        private void HandleWeaponUnequipped()
        {
            ClearEffect();
            weaponRenderers = null;
        }

        private System.Collections.IEnumerator UpdateVisualsNextFrame()
        {
            yield return null;
            RefreshWeaponRenderers();
            UpdateVisuals();
        }

        private void RefreshWeaponRenderers()
        {
            if (playerEquipment == null) return;

            // Find renderers in the weapon view socket
            var weaponViewSocket = playerEquipment.transform.Find("CameraRoot/Camera/WeaponViewSocket");
            if (weaponViewSocket != null)
            {
                weaponRenderers = weaponViewSocket.GetComponentsInChildren<Renderer>(true);
            }
            else
            {
                // Fallback: search for MeshRenderer children
                weaponRenderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void UpdateVisuals()
        {
            if (!playerEquipment.HasWeaponEquipped) return;

            var condition = playerEquipment.CurrentEquipment.Condition;
            if (EquipmentConditionHelper.IsBroken(condition))
            {
                ApplyBrokenEffect();
            }
            else
            {
                ClearEffect();
            }
        }

        /// <summary>
        /// Apply broken visual effect to weapon renderers.
        /// </summary>
        public void ApplyBrokenEffect()
        {
            if (weaponRenderers == null || weaponRenderers.Length == 0)
            {
                RefreshWeaponRenderers();
            }

            if (weaponRenderers == null || weaponRenderers.Length == 0)
            {
                return;
            }

            // Calculate the tinted, desaturated color
            // We modify the base color to simulate desaturation and tinting
            Color modifiedColor = CalculateBrokenColor(Color.white);

            foreach (var renderer in weaponRenderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(propertyBlock);

                // Try both common color property names
                propertyBlock.SetColor(ColorPropertyId, modifiedColor);
                propertyBlock.SetColor(TintPropertyId, modifiedColor);

                renderer.SetPropertyBlock(propertyBlock);
            }

            isEffectApplied = true;
            LootboundLog.Info(Category, "Broken visual effect applied to weapon");
        }

        /// <summary>
        /// Clear all visual effects from weapon renderers.
        /// </summary>
        public void ClearEffect()
        {
            if (weaponRenderers == null || !isEffectApplied) return;

            foreach (var renderer in weaponRenderers)
            {
                if (renderer == null) continue;

                // Clear the property block to restore original material properties
                renderer.SetPropertyBlock(null);
            }

            isEffectApplied = false;
            LootboundLog.Info(Category, "Weapon visual effect cleared");
        }

        private Color CalculateBrokenColor(Color originalColor)
        {
            // Calculate grayscale (luminance)
            float gray = originalColor.r * 0.299f + originalColor.g * 0.587f + originalColor.b * 0.114f;

            // Interpolate between original and grayscale
            Color desaturated = new Color(
                Mathf.Lerp(originalColor.r, gray, brokenDesaturation),
                Mathf.Lerp(originalColor.g, gray, brokenDesaturation),
                Mathf.Lerp(originalColor.b, gray, brokenDesaturation),
                originalColor.a
            );

            // Apply tint
            Color tinted = Color.Lerp(desaturated, brokenTint, brokenTintStrength);
            tinted.a = originalColor.a;

            return tinted;
        }

        /// <summary>
        /// Force refresh the visual state.
        /// </summary>
        public void ForceRefresh()
        {
            RefreshWeaponRenderers();
            UpdateVisuals();
        }
    }
}
