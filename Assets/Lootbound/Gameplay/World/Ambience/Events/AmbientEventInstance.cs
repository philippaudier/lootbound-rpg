using UnityEngine;

namespace Lootbound.Gameplay.World.Ambience.Events
{
    /// <summary>
    /// One live ambient event: a temporary, world-anchored presence around
    /// the player. Pure runtime class - the director creates a bare marker
    /// GameObject and stores its transform here; presentation layers attach
    /// whatever they need to it through the director's events.
    /// </summary>
    public sealed class AmbientEventInstance
    {
        public AmbientEventProfile Profile { get; }
        public Vector3 Position { get; }
        public float SpawnTime { get; }
        public float ExpirationTime { get; }
        public Transform MarkerTransform { get; }

        public AmbientEventInstance(
            AmbientEventProfile profile, Vector3 position,
            float spawnTime, float expirationTime, Transform markerTransform)
        {
            Profile = profile;
            Position = position;
            SpawnTime = spawnTime;
            ExpirationTime = expirationTime;
            MarkerTransform = markerTransform;
        }

        public float RemainingSeconds(float now) => Mathf.Max(0f, ExpirationTime - now);
    }
}
