using System;
using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Controls the attunement ritual presentation sequence.
    /// Manages state transitions, timing, visual feedback, and audio coordination.
    /// </summary>
    /// <remarks>
    /// The ritual follows these phases:
    /// 1. Preparing (0.2s) - Initial activation, weapon appears on table
    /// 2. Building (0.5s) - Tension rises, emission intensifies
    /// 3. Resolving (0.4s) - Peak moment, result determined (but already known)
    /// 4. ShowingResult (0.4s) - Success/failure revealed with feedback
    ///
    /// Total duration: ~1.5s (configurable via AttunementRitualConfig)
    ///
    /// IMPORTANT: The actual attunement transaction is resolved BEFORE the ritual
    /// starts. The ritual only presents the pre-determined result.
    /// </remarks>
    public class AttunementRitualController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private AttunementRitualConfig config;

        [Header("Table References")]
        [SerializeField] private Transform weaponAnchor;
        [SerializeField] private Transform stoneAnchor;
        [SerializeField] private Renderer tableRenderer;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        // State
        private AttunementRitualState currentState = AttunementRitualState.Idle;
        private float stateTimer;
        private float stateDuration;

        // Ritual data (set before starting)
        private bool pendingSuccess;
        private bool pendingGuaranteed;
        private int pendingPreviousLevel;
        private int pendingNewLevel;

        // Visual instances
        private GameObject weaponInstance;
        private GameObject stoneInstance;

        // Emission
        private MaterialPropertyBlock propertyBlock;
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
        private Color currentEmissionColor;
        private float currentEmissionIntensity;

        // Events
        /// <summary>
        /// Raised when the ritual completes and result should be shown.
        /// Parameters: success, wasGuaranteed, previousLevel, newLevel
        /// </summary>
        public event Action<bool, bool, int, int> OnRitualComplete;

        /// <summary>
        /// Raised when the ritual is cancelled.
        /// </summary>
        public event Action OnRitualCancelled;

        /// <summary>
        /// Current state of the ritual.
        /// </summary>
        public AttunementRitualState CurrentState => currentState;

        /// <summary>
        /// Whether a ritual is currently in progress.
        /// </summary>
        public bool IsRitualInProgress => currentState != AttunementRitualState.Idle;

        /// <summary>
        /// Progress through the current phase (0-1).
        /// </summary>
        public float CurrentPhaseProgress => stateDuration > 0f ? 1f - (stateTimer / stateDuration) : 0f;

        /// <summary>
        /// Total ritual progress (0-1).
        /// </summary>
        public float TotalProgress
        {
            get
            {
                if (config == null || currentState == AttunementRitualState.Idle)
                    return 0f;

                float elapsed = 0f;
                float total = config.TotalDuration;

                switch (currentState)
                {
                    case AttunementRitualState.Preparing:
                        elapsed = (config.PreparingDuration - stateTimer);
                        break;
                    case AttunementRitualState.Building:
                        elapsed = config.PreparingDuration + (config.BuildingDuration - stateTimer);
                        break;
                    case AttunementRitualState.Resolving:
                        elapsed = config.PreparingDuration + config.BuildingDuration + (config.ResolvingDuration - stateTimer);
                        break;
                    case AttunementRitualState.ShowingResult:
                        elapsed = config.PreparingDuration + config.BuildingDuration + config.ResolvingDuration + (config.ShowingResultDuration - stateTimer);
                        break;
                }

                return Mathf.Clamp01(elapsed / total);
            }
        }

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                }
            }
        }

        private void Update()
        {
            if (currentState == AttunementRitualState.Idle)
                return;

            stateTimer -= Time.deltaTime;

            // Update visual effects based on current state
            UpdateVisuals();

            // Check for state transition
            if (stateTimer <= 0f)
            {
                TransitionToNextState();
            }
        }

        /// <summary>
        /// Start the attunement ritual with a pre-determined result.
        /// </summary>
        /// <param name="success">Whether the attunement succeeded.</param>
        /// <param name="wasGuaranteed">Whether success was guaranteed.</param>
        /// <param name="previousLevel">Level before attempt.</param>
        /// <param name="newLevel">Level after attempt.</param>
        /// <param name="weaponPrefab">Optional prefab to display on the table.</param>
        public void StartRitual(bool success, bool wasGuaranteed, int previousLevel, int newLevel, GameObject weaponPrefab = null)
        {
            if (config == null)
            {
                Debug.LogWarning("[AttunementRitual] No config assigned, skipping ritual.");
                OnRitualComplete?.Invoke(success, wasGuaranteed, previousLevel, newLevel);
                return;
            }

            if (currentState != AttunementRitualState.Idle)
            {
                Debug.LogWarning("[AttunementRitual] Ritual already in progress.");
                return;
            }

            // Store pending result
            pendingSuccess = success;
            pendingGuaranteed = wasGuaranteed;
            pendingPreviousLevel = previousLevel;
            pendingNewLevel = newLevel;

            // Spawn weapon visual if prefab provided
            if (weaponPrefab != null && weaponAnchor != null)
            {
                SpawnWeaponVisual(weaponPrefab);
            }

            // Spawn stone visual if prefab configured
            if (config.StonePrefab != null && stoneAnchor != null)
            {
                SpawnStoneVisual(config.StonePrefab);
            }

            // Initialize emission
            currentEmissionColor = config.EmissionColor;
            currentEmissionIntensity = config.BaseEmissionIntensity;
            ApplyEmission();

            // Start first phase
            SetState(AttunementRitualState.Preparing);

            // Play start sound
            PlaySound(config.RitualStartClip, "RitualStart");

            if (config.EnableDebugLogs)
            {
                Debug.Log($"[AttunementRitual] Started ritual: success={success}, guaranteed={wasGuaranteed}, {previousLevel}→{newLevel}");
            }
        }

        /// <summary>
        /// Cancel the current ritual immediately.
        /// </summary>
        public void CancelRitual()
        {
            if (currentState == AttunementRitualState.Idle)
                return;

            SetState(AttunementRitualState.Cancelling);

            // Clean up immediately
            CleanupVisuals();
            ResetEmission();

            SetState(AttunementRitualState.Idle);

            OnRitualCancelled?.Invoke();

            if (config != null && config.EnableDebugLogs)
            {
                Debug.Log("[AttunementRitual] Ritual cancelled.");
            }
        }

        private void SetState(AttunementRitualState newState)
        {
            currentState = newState;

            switch (newState)
            {
                case AttunementRitualState.Preparing:
                    stateDuration = config.PreparingDuration;
                    break;
                case AttunementRitualState.Building:
                    stateDuration = config.BuildingDuration;
                    // Start looping build sound if available
                    if (config.RitualBuildClip != null && audioSource != null)
                    {
                        audioSource.clip = config.RitualBuildClip;
                        audioSource.loop = true;
                        audioSource.volume = config.MasterVolume;
                        audioSource.pitch = config.GetRandomPitch();
                        audioSource.Play();
                    }
                    break;
                case AttunementRitualState.Resolving:
                    stateDuration = config.ResolvingDuration;
                    // Stop looping sound
                    if (audioSource != null && audioSource.isPlaying && audioSource.loop)
                    {
                        audioSource.Stop();
                        audioSource.loop = false;
                    }
                    break;
                case AttunementRitualState.ShowingResult:
                    stateDuration = config.ShowingResultDuration;
                    // Play result sound
                    if (pendingSuccess)
                    {
                        if (pendingGuaranteed)
                        {
                            PlaySound(config.GuaranteedSuccessClip, "GuaranteedSuccess");
                        }
                        else
                        {
                            PlaySound(config.SuccessClip, "Success");
                        }
                    }
                    else
                    {
                        PlaySound(config.FailureClip, "Failure");
                    }
                    break;
                default:
                    stateDuration = 0f;
                    break;
            }

            stateTimer = stateDuration;

            if (config != null && config.EnableDebugLogs)
            {
                Debug.Log($"[AttunementRitual] State: {newState}, duration: {stateDuration:F2}s");
            }
        }

        private void TransitionToNextState()
        {
            switch (currentState)
            {
                case AttunementRitualState.Preparing:
                    SetState(AttunementRitualState.Building);
                    break;
                case AttunementRitualState.Building:
                    SetState(AttunementRitualState.Resolving);
                    break;
                case AttunementRitualState.Resolving:
                    SetState(AttunementRitualState.ShowingResult);
                    break;
                case AttunementRitualState.ShowingResult:
                    CompleteRitual();
                    break;
            }
        }

        private void CompleteRitual()
        {
            CleanupVisuals();
            ResetEmission();

            var success = pendingSuccess;
            var guaranteed = pendingGuaranteed;
            var prevLevel = pendingPreviousLevel;
            var newLevel = pendingNewLevel;

            SetState(AttunementRitualState.Idle);

            OnRitualComplete?.Invoke(success, guaranteed, prevLevel, newLevel);

            if (config != null && config.EnableDebugLogs)
            {
                Debug.Log($"[AttunementRitual] Ritual complete: success={success}");
            }
        }

        private void UpdateVisuals()
        {
            if (config == null)
                return;

            float phase = CurrentPhaseProgress;

            switch (currentState)
            {
                case AttunementRitualState.Preparing:
                    // Fade in emission
                    currentEmissionIntensity = Mathf.Lerp(0f, config.BaseEmissionIntensity, phase);
                    currentEmissionColor = config.EmissionColor;
                    break;

                case AttunementRitualState.Building:
                    // Ramp up intensity
                    currentEmissionIntensity = Mathf.Lerp(config.BaseEmissionIntensity, config.PeakEmissionIntensity, phase);
                    currentEmissionColor = config.EmissionColor;
                    break;

                case AttunementRitualState.Resolving:
                    // Peak intensity, slight pulse
                    float pulse = 1f + Mathf.Sin(phase * Mathf.PI * 4f) * 0.1f;
                    currentEmissionIntensity = config.PeakEmissionIntensity * pulse;
                    currentEmissionColor = config.EmissionColor;
                    break;

                case AttunementRitualState.ShowingResult:
                    // Flash to result color then fade
                    if (pendingSuccess)
                    {
                        float flash = phase < 0.3f ? Mathf.Lerp(config.SuccessFlashIntensity, config.PeakEmissionIntensity, phase / 0.3f) : Mathf.Lerp(config.PeakEmissionIntensity, config.BaseEmissionIntensity, (phase - 0.3f) / 0.7f);
                        currentEmissionIntensity = flash;
                        currentEmissionColor = Color.Lerp(config.SuccessEmissionColor, config.EmissionColor, phase);
                    }
                    else
                    {
                        currentEmissionIntensity = Mathf.Lerp(config.PeakEmissionIntensity, config.BaseEmissionIntensity * 0.5f, phase);
                        currentEmissionColor = Color.Lerp(config.FailureEmissionColor, config.EmissionColor, phase);
                    }
                    break;
            }

            ApplyEmission();
            UpdateWeaponAnimation();
            UpdateStoneAnimation();
        }

        private void ApplyEmission()
        {
            if (tableRenderer == null)
                return;

            tableRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorProperty, currentEmissionColor * currentEmissionIntensity);
            tableRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ResetEmission()
        {
            if (tableRenderer == null)
                return;

            tableRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorProperty, Color.black);
            tableRenderer.SetPropertyBlock(propertyBlock);
        }

        private void SpawnWeaponVisual(GameObject prefab)
        {
            if (weaponAnchor == null)
                return;

            // Clean up any existing
            if (weaponInstance != null)
            {
                Destroy(weaponInstance);
            }

            weaponInstance = Instantiate(prefab, weaponAnchor.position + Vector3.up * config.WeaponHeightOffset, weaponAnchor.rotation, weaponAnchor);

            // Disable any physics/interaction
            var rigidbody = weaponInstance.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.isKinematic = true;
            }

            var colliders = weaponInstance.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
        }

        private void UpdateWeaponAnimation()
        {
            if (weaponInstance == null || config == null)
                return;

            // Float animation
            float floatOffset = Mathf.Sin(Time.time * 3f) * config.WeaponFloatAmplitude;
            weaponInstance.transform.localPosition = Vector3.up * (config.WeaponHeightOffset + floatOffset);

            // Rotation
            weaponInstance.transform.Rotate(Vector3.up, config.WeaponRotationSpeed * Time.deltaTime, Space.Self);
        }

        private void SpawnStoneVisual(GameObject prefab)
        {
            if (stoneAnchor == null)
                return;

            // Clean up any existing
            if (stoneInstance != null)
            {
                Destroy(stoneInstance);
            }

            stoneInstance = Instantiate(prefab, stoneAnchor.position + Vector3.up * config.StoneHeightOffset, stoneAnchor.rotation, stoneAnchor);

            // Disable any physics/interaction
            var rigidbody = stoneInstance.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.isKinematic = true;
            }

            var colliders = stoneInstance.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
        }

        private void UpdateStoneAnimation()
        {
            if (stoneInstance == null || config == null)
                return;

            // Float animation (slightly faster than weapon)
            float floatOffset = Mathf.Sin(Time.time * 4f) * config.StoneFloatAmplitude;
            stoneInstance.transform.localPosition = Vector3.up * (config.StoneHeightOffset + floatOffset);

            // Rotation (faster than weapon)
            stoneInstance.transform.Rotate(Vector3.up, config.StoneRotationSpeed * Time.deltaTime, Space.Self);
        }

        private void CleanupVisuals()
        {
            if (weaponInstance != null)
            {
                Destroy(weaponInstance);
                weaponInstance = null;
            }

            if (stoneInstance != null)
            {
                Destroy(stoneInstance);
                stoneInstance = null;
            }

            // Stop any looping audio
            if (audioSource != null && audioSource.isPlaying && audioSource.loop)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }
        }

        private void PlaySound(AudioClip clip, string eventName)
        {
            if (audioSource == null)
                return;

            if (clip != null)
            {
                audioSource.pitch = config.GetRandomPitch();
                audioSource.PlayOneShot(clip, config.MasterVolume);
            }
            else if (config != null && config.EnableDebugLogs)
            {
                Debug.Log($"[AttunementRitual] {eventName} (placeholder - no clip assigned)");
            }
        }

        private void OnDisable()
        {
            if (currentState != AttunementRitualState.Idle)
            {
                CancelRitual();
            }
        }

        private void OnDestroy()
        {
            CleanupVisuals();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor helper to test ritual phases.
        /// </summary>
        [ContextMenu("Test Ritual (Success)")]
        private void TestRitualSuccess()
        {
            StartRitual(true, false, 0, 1, null);
        }

        [ContextMenu("Test Ritual (Failure)")]
        private void TestRitualFailure()
        {
            StartRitual(false, false, 3, 3, null);
        }

        [ContextMenu("Test Ritual (Guaranteed)")]
        private void TestRitualGuaranteed()
        {
            StartRitual(true, true, 4, 5, null);
        }
#endif
    }
}
