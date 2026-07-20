using System;
using UnityEngine;

namespace Lootbound.Presentation.Wildlife
{
    /// <summary>One visual bird variant. Null prefabs and non-positive weights are ignored at selection time.</summary>
    [Serializable]
    public class BirdVisualVariant
    {
        [SerializeField]
        [Tooltip("Very light bird prefab (simple mesh or silhouette). Null entries are ignored.")]
        private GameObject prefab;

        [SerializeField, Min(0f)]
        [Tooltip("Relative selection weight; entries with weight <= 0 are ignored")]
        private float weight = 1f;

        [SerializeField]
        [Tooltip("Uniform scale range per bird")]
        private Vector2 scaleRange = new Vector2(0.8f, 1.2f);

        [SerializeField]
        [Tooltip("Wing-flap speed range (cycles per second, used by the fallback flap animation)")]
        private Vector2 flapSpeedRange = new Vector2(2f, 4f);

        public GameObject Prefab => prefab;
        public float Weight => weight;

        public Vector2 ScaleRange
        {
            get
            {
                float min = Mathf.Max(0.05f, Mathf.Min(scaleRange.x, scaleRange.y));
                float max = Mathf.Max(min, Mathf.Max(scaleRange.x, scaleRange.y));
                return new Vector2(min, max);
            }
        }

        public Vector2 FlapSpeedRange
        {
            get
            {
                float min = Mathf.Max(0.1f, Mathf.Min(flapSpeedRange.x, flapSpeedRange.y));
                float max = Mathf.Max(min, Mathf.Max(flapSpeedRange.x, flapSpeedRange.y));
                return new Vector2(min, max);
            }
        }
    }

    /// <summary>
    /// Authoring data for the visible birds - the visual sibling of
    /// BirdAudioLibrary. Every accessor is defensively bounded; the system
    /// stays stable with a missing or empty library.
    /// </summary>
    [CreateAssetMenu(fileName = "BirdVisualLibrary", menuName = "Lootbound/Wildlife/Bird Visual Library")]
    public class BirdVisualLibrary : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Visual variants; leave empty until real prefabs exist")]
        private BirdVisualVariant[] variants = new BirdVisualVariant[0];

        [Header("Flock")]
        [SerializeField]
        [Tooltip("Birds per flock (min..max, inclusive)")]
        private Vector2Int groupSizeRange = new Vector2Int(2, 6);

        [SerializeField]
        [Tooltip("Seconds a flight lasts at speed factor 1 (min..max)")]
        private Vector2 flightDurationRange = new Vector2(6f, 12f);

        [SerializeField, Min(4f)]
        [Tooltip("Horizontal radius of the flight around the event position, in meters")]
        private float flightRadius = 18f;

        [SerializeField]
        [Tooltip("Flight height above the resolved ground (min..max, meters)")]
        private Vector2 flightHeightRange = new Vector2(4f, 14f);

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Vertical oscillation amplitude, in meters")]
        private float bobAmplitude = 0.35f;

        public BirdVisualVariant[] Variants => variants;

        public Vector2Int GroupSizeRange
        {
            get
            {
                int min = Mathf.Clamp(Mathf.Min(groupSizeRange.x, groupSizeRange.y), 1, 12);
                int max = Mathf.Clamp(Mathf.Max(groupSizeRange.x, groupSizeRange.y), min, 12);
                return new Vector2Int(min, max);
            }
        }

        public Vector2 FlightDurationRange
        {
            get
            {
                float min = Mathf.Clamp(Mathf.Min(flightDurationRange.x, flightDurationRange.y), 1f, 120f);
                float max = Mathf.Clamp(Mathf.Max(flightDurationRange.x, flightDurationRange.y), min, 120f);
                return new Vector2(min, max);
            }
        }

        public float FlightRadius => Mathf.Max(4f, flightRadius);

        public Vector2 FlightHeightRange
        {
            get
            {
                float min = Mathf.Max(0.5f, Mathf.Min(flightHeightRange.x, flightHeightRange.y));
                float max = Mathf.Max(min, Mathf.Max(flightHeightRange.x, flightHeightRange.y));
                return new Vector2(min, max);
            }
        }

        public float BobAmplitude => Mathf.Clamp(bobAmplitude, 0f, 2f);

        private void OnValidate()
        {
            if (flightRadius < 4f) flightRadius = 4f;
            groupSizeRange.x = Mathf.Clamp(groupSizeRange.x, 1, 12);
            groupSizeRange.y = Mathf.Clamp(groupSizeRange.y, groupSizeRange.x, 12);
        }
    }
}
