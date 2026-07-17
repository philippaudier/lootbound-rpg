using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Plays audio feedback for equipment condition changes and repairs.
    /// Slice 0.7.7: Placeholder system with console logging for missing clips.
    /// </summary>
    public class EquipmentAudioFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerWeaponWear playerWeaponWear;
        [SerializeField] private PlayerRepair playerRepair;
        [SerializeField] private AudioSource audioSource;

        [Header("Condition Change Sounds")]
        [Tooltip("Sound when condition degrades (Good, Worn, Fragile)")]
        [SerializeField] private AudioClip conditionDegradeClip;

        [Tooltip("Sound when weapon breaks")]
        [SerializeField] private AudioClip weaponBreakClip;

        [Header("Repair Sounds")]
        [Tooltip("Sound when repair completes successfully")]
        [SerializeField] private AudioClip repairCompleteClip;

        [Tooltip("Sound when equipment is restored from Broken state")]
        [SerializeField] private AudioClip restoredFromBrokenClip;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float conditionChangeVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float breakVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float repairVolume = 0.7f;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f; // 2D sound for UI feedback
                }
            }
        }

        private void OnEnable()
        {
            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnConditionChanged += HandleConditionChanged;
            }

            if (playerRepair != null)
            {
                playerRepair.OnRepairCompleted += HandleRepairCompleted;
            }
        }

        private void OnDisable()
        {
            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnConditionChanged -= HandleConditionChanged;
            }

            if (playerRepair != null)
            {
                playerRepair.OnRepairCompleted -= HandleRepairCompleted;
            }
        }

        private void HandleConditionChanged(WearResult result)
        {
            if (!result.ConditionChanged)
            {
                return;
            }

            if (result.NowBroken)
            {
                PlaySound(weaponBreakClip, breakVolume, "WeaponBreak");
            }
            else
            {
                PlaySound(conditionDegradeClip, conditionChangeVolume, "ConditionDegrade");
            }
        }

        private void HandleRepairCompleted(RepairResult result)
        {
            if (!result.Success)
            {
                return;
            }

            if (result.RestoredFromBroken)
            {
                PlaySound(restoredFromBrokenClip, repairVolume, "RestoredFromBroken");
            }
            else
            {
                PlaySound(repairCompleteClip, repairVolume, "RepairComplete");
            }
        }

        private void PlaySound(AudioClip clip, float volume, string eventName)
        {
            if (clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
            else
            {
                // Slice 0.7.7: Placeholder logging for missing clips
                Debug.Log($"[EquipmentAudio] {eventName} (placeholder - no clip assigned)");
            }
        }
    }
}
