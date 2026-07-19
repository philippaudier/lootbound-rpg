using UnityEngine;

namespace Lootbound.Presentation.Audio
{
    /// <summary>
    /// Authoring data for the bird voice: clips and 3D source settings.
    /// One library for the whole world in V1. Every accessor is defensively
    /// sanitized so a badly authored asset can never produce an invalid
    /// AudioSource.
    /// </summary>
    [CreateAssetMenu(fileName = "BirdAudioLibrary", menuName = "Lootbound/Audio/Bird Audio Library")]
    public class BirdAudioLibrary : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Candidate chirp clips; null entries are ignored at selection time")]
        private AudioClip[] clips = new AudioClip[0];

        [SerializeField]
        [Tooltip("Random pitch per event (the same bird never sings exactly the same)")]
        private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        [SerializeField]
        [Tooltip("Random volume per event")]
        private Vector2 volumeRange = new Vector2(0.90f, 1.00f);

        [SerializeField, Min(0.1f)]
        [Tooltip("Distance under which the source plays at full volume")]
        private float minDistance = 8f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Distance beyond which the source is inaudible")]
        private float maxDistance = 45f;

        [SerializeField]
        private AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("1 = fully 3D (the sound belongs to the world)")]
        private float spatialBlend = 1f;

        [SerializeField, Range(0, 256)]
        private int priority = 128;

        public AudioClip[] Clips => clips;

        public Vector2 PitchRange
        {
            get
            {
                float min = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
                float max = Mathf.Max(min, Mathf.Max(pitchRange.x, pitchRange.y));
                return new Vector2(min, max);
            }
        }

        public Vector2 VolumeRange
        {
            get
            {
                float min = Mathf.Clamp01(Mathf.Min(volumeRange.x, volumeRange.y));
                float max = Mathf.Clamp01(Mathf.Max(volumeRange.x, volumeRange.y));
                return new Vector2(min, max);
            }
        }

        public float MinDistance => Mathf.Max(0.1f, Mathf.Min(minDistance, maxDistance));
        public float MaxDistance => Mathf.Max(MinDistance, Mathf.Max(minDistance, maxDistance));
        public AudioRolloffMode Rolloff => rolloff;
        public float SpatialBlend => Mathf.Clamp01(spatialBlend);
        public int Priority => Mathf.Clamp(priority, 0, 256);

        private void OnValidate()
        {
            if (minDistance < 0.1f) minDistance = 0.1f;
            if (maxDistance < minDistance) maxDistance = minDistance;
            if (pitchRange.x <= 0f) pitchRange.x = 0.01f;
            if (pitchRange.y <= 0f) pitchRange.y = 0.01f;
            volumeRange.x = Mathf.Clamp01(volumeRange.x);
            volumeRange.y = Mathf.Clamp01(volumeRange.y);
            priority = Mathf.Clamp(priority, 0, 256);
        }
    }
}
