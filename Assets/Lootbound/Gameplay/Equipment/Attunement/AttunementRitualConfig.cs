using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Configuration for the attunement ritual presentation.
    /// Controls timings, audio, and visual feedback settings.
    /// </summary>
    [CreateAssetMenu(fileName = "AttunementRitualConfig", menuName = "Lootbound/Equipment/Attunement Ritual Config")]
    public class AttunementRitualConfig : ScriptableObject
    {
        [Header("Phase Durations")]
        [Tooltip("Duration of the preparing phase (initial activation).")]
        [SerializeField, Range(0.05f, 0.5f)] private float preparingDuration = 0.2f;

        [Tooltip("Duration of the building phase (tension rising).")]
        [SerializeField, Range(0.2f, 1.0f)] private float buildingDuration = 0.5f;

        [Tooltip("Duration of the resolving phase (peak moment).")]
        [SerializeField, Range(0.2f, 0.8f)] private float resolvingDuration = 0.4f;

        [Tooltip("Duration of the result display phase.")]
        [SerializeField, Range(0.2f, 0.8f)] private float showingResultDuration = 0.4f;

        [Header("Audio Clips")]
        [Tooltip("Sound when ritual starts.")]
        [SerializeField] private AudioClip ritualStartClip;

        [Tooltip("Optional looping sound during building phase.")]
        [SerializeField] private AudioClip ritualBuildClip;

        [Tooltip("Sound on successful attunement.")]
        [SerializeField] private AudioClip successClip;

        [Tooltip("Sound on failed attunement.")]
        [SerializeField] private AudioClip failureClip;

        [Tooltip("Sound on guaranteed success.")]
        [SerializeField] private AudioClip guaranteedSuccessClip;

        [Header("Audio Settings")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.7f;
        [SerializeField] private Vector2 pitchVariation = new Vector2(0.95f, 1.05f);

        [Header("Visual Settings")]
        [Tooltip("Base emission intensity at rest.")]
        [SerializeField, Range(0f, 1f)] private float baseEmissionIntensity = 0.1f;

        [Tooltip("Peak emission intensity during resolving phase.")]
        [SerializeField, Range(0.5f, 3f)] private float peakEmissionIntensity = 1.5f;

        [Tooltip("Success flash emission intensity.")]
        [SerializeField, Range(1f, 5f)] private float successFlashIntensity = 2.5f;

        [Tooltip("Emission color for the table.")]
        [SerializeField] private Color emissionColor = new Color(1f, 0.85f, 0.5f); // Warm gold

        [Tooltip("Success emission color.")]
        [SerializeField] private Color successEmissionColor = new Color(0.8f, 1f, 0.8f); // Soft green

        [Tooltip("Failure emission color.")]
        [SerializeField] private Color failureEmissionColor = new Color(0.6f, 0.5f, 0.5f); // Muted

        [Header("Weapon Display")]
        [Tooltip("Height offset for weapon above anchor.")]
        [SerializeField] private float weaponHeightOffset = 0.05f;

        [Tooltip("Weapon float amplitude during ritual.")]
        [SerializeField, Range(0f, 0.1f)] private float weaponFloatAmplitude = 0.02f;

        [Tooltip("Weapon rotation speed (degrees per second).")]
        [SerializeField, Range(0f, 90f)] private float weaponRotationSpeed = 15f;

        [Header("Stone Display")]
        [Tooltip("Prefab for visual stone during ritual (optional).")]
        [SerializeField] private GameObject stonePrefab;

        [Tooltip("Height offset for stone above anchor.")]
        [SerializeField] private float stoneHeightOffset = 0.1f;

        [Tooltip("Stone float amplitude during ritual.")]
        [SerializeField, Range(0f, 0.1f)] private float stoneFloatAmplitude = 0.03f;

        [Tooltip("Stone rotation speed (degrees per second).")]
        [SerializeField, Range(0f, 180f)] private float stoneRotationSpeed = 30f;

        [Header("Camera")]
        [Tooltip("Enable subtle camera focus during ritual.")]
        [SerializeField] private bool enableCameraFocus = true;

        [Tooltip("FOV reduction during ritual (degrees).")]
        [SerializeField, Range(0f, 15f)] private float fovReduction = 5f;

        [Tooltip("Camera transition duration.")]
        [SerializeField, Range(0.1f, 0.5f)] private float cameraTransitionDuration = 0.25f;

        [Header("Camera Shake")]
        [Tooltip("Enable micro shake on success.")]
        [SerializeField] private bool enableSuccessShake = true;

        [Tooltip("Success shake intensity.")]
        [SerializeField, Range(0f, 0.1f)] private float successShakeIntensity = 0.02f;

        [Tooltip("Success shake duration.")]
        [SerializeField, Range(0.05f, 0.3f)] private float successShakeDuration = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Public accessors
        public float PreparingDuration => preparingDuration;
        public float BuildingDuration => buildingDuration;
        public float ResolvingDuration => resolvingDuration;
        public float ShowingResultDuration => showingResultDuration;

        /// <summary>
        /// Total ritual duration from start to result shown.
        /// </summary>
        public float TotalDuration => preparingDuration + buildingDuration + resolvingDuration + showingResultDuration;

        // Audio
        public AudioClip RitualStartClip => ritualStartClip;
        public AudioClip RitualBuildClip => ritualBuildClip;
        public AudioClip SuccessClip => successClip;
        public AudioClip FailureClip => failureClip;
        public AudioClip GuaranteedSuccessClip => guaranteedSuccessClip ?? successClip;
        public float MasterVolume => masterVolume;
        public Vector2 PitchVariation => pitchVariation;

        // Visual
        public float BaseEmissionIntensity => baseEmissionIntensity;
        public float PeakEmissionIntensity => peakEmissionIntensity;
        public float SuccessFlashIntensity => successFlashIntensity;
        public Color EmissionColor => emissionColor;
        public Color SuccessEmissionColor => successEmissionColor;
        public Color FailureEmissionColor => failureEmissionColor;

        // Weapon
        public float WeaponHeightOffset => weaponHeightOffset;
        public float WeaponFloatAmplitude => weaponFloatAmplitude;
        public float WeaponRotationSpeed => weaponRotationSpeed;

        // Stone
        public GameObject StonePrefab => stonePrefab;
        public float StoneHeightOffset => stoneHeightOffset;
        public float StoneFloatAmplitude => stoneFloatAmplitude;
        public float StoneRotationSpeed => stoneRotationSpeed;

        // Camera
        public bool EnableCameraFocus => enableCameraFocus;
        public float FovReduction => fovReduction;
        public float CameraTransitionDuration => cameraTransitionDuration;
        public bool EnableSuccessShake => enableSuccessShake;
        public float SuccessShakeIntensity => successShakeIntensity;
        public float SuccessShakeDuration => successShakeDuration;

        // Debug
        public bool EnableDebugLogs => enableDebugLogs;

        /// <summary>
        /// Get a randomized pitch value within the variation range.
        /// </summary>
        public float GetRandomPitch()
        {
            return Random.Range(pitchVariation.x, pitchVariation.y);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure minimum durations
            preparingDuration = Mathf.Max(0.05f, preparingDuration);
            buildingDuration = Mathf.Max(0.1f, buildingDuration);
            resolvingDuration = Mathf.Max(0.1f, resolvingDuration);
            showingResultDuration = Mathf.Max(0.1f, showingResultDuration);

            // Ensure pitch variation is valid
            if (pitchVariation.x > pitchVariation.y)
            {
                pitchVariation = new Vector2(pitchVariation.y, pitchVariation.x);
            }
        }
#endif
    }
}
