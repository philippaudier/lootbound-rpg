using UnityEngine;

namespace Lootbound.Gameplay.World.Ambience.Events
{
    /// <summary>
    /// Authoring definition of one ambient event family. Pure data: no audio
    /// clip, no prefab - presentation layers subscribe to the director's
    /// events and decide what a spawned instance sounds or looks like.
    /// </summary>
    [CreateAssetMenu(fileName = "AmbientEventProfile", menuName = "Lootbound/World/Ambient Event Profile")]
    public class AmbientEventProfile : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Stable identifier of this event family")]
        private string eventId = "";

        [SerializeField]
        private AmbientEventCategory category = AmbientEventCategory.Birds;

        [SerializeField, Min(0f)]
        [Tooltip("Relative weight against other eligible profiles")]
        private float weight = 1f;

        [SerializeField]
        [Tooltip("How the category activity (0..1) translates into eligibility. Output is clamped to 0..1 and multiplies the weight - the single place where activity is applied.")]
        private AnimationCurve activityResponse = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField]
        [Tooltip("Seconds this profile stays ineligible after spawning (min..max)")]
        private Vector2 cooldownRange = new Vector2(8f, 20f);

        [SerializeField]
        [Tooltip("Seconds an instance stays active (min..max)")]
        private Vector2 lifetimeRange = new Vector2(4f, 10f);

        [SerializeField]
        [Tooltip("Horizontal distance from the player (min..max, meters)")]
        private Vector2 distanceRange = new Vector2(8f, 30f);

        [SerializeField]
        [Tooltip("Vertical offset above the resolved ground (min..max, meters)")]
        private Vector2 heightOffsetRange = new Vector2(0.5f, 6f);

        [SerializeField, Min(1)]
        [Tooltip("Maximum simultaneous instances of this profile")]
        private int maxConcurrent = 1;

        [SerializeField]
        [Tooltip("When enabled, positions inside the player's frontal view cone are excluded")]
        private bool avoidPlayerView;

        public string EventId => eventId;
        public AmbientEventCategory Category => category;
        public float Weight => weight;
        public Vector2 CooldownRange => Ordered(cooldownRange, 0f);
        public Vector2 LifetimeRange => Ordered(lifetimeRange, 0.1f);
        public Vector2 DistanceRange => Ordered(distanceRange, 0.5f);
        public Vector2 HeightOffsetRange => OrderedSigned(heightOffsetRange);
        public int MaxConcurrent => Mathf.Max(1, maxConcurrent);
        public bool AvoidPlayerView => avoidPlayerView;

        /// <summary>
        /// The SINGLE application of activity: clamped curve output, later
        /// multiplied by the weight in the selector. Activity is never
        /// applied anywhere else.
        /// </summary>
        public float EvaluateResponse(float activity)
        {
            float sane = float.IsNaN(activity) ? 0f : Mathf.Clamp01(activity);
            return activityResponse != null
                ? Mathf.Clamp01(activityResponse.Evaluate(sane))
                : sane;
        }

        private static Vector2 Ordered(Vector2 range, float floor)
        {
            float min = Mathf.Max(floor, Mathf.Min(range.x, range.y));
            float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return new Vector2(min, max);
        }

        private static Vector2 OrderedSigned(Vector2 range)
        {
            return range.x <= range.y ? range : new Vector2(range.y, range.x);
        }

        private void OnValidate()
        {
            if (weight < 0f) weight = 0f;
            if (maxConcurrent < 1) maxConcurrent = 1;
            if (cooldownRange.x < 0f) cooldownRange.x = 0f;
            if (lifetimeRange.x < 0.1f) lifetimeRange.x = 0.1f;
            if (distanceRange.x < 0.5f) distanceRange.x = 0.5f;
        }
    }
}
