using UnityEngine;

namespace Lootbound.Gameplay.World.Ambience.Events
{
    /// <summary>Resolves a ground height at a world XZ. Returns false when no ground is known.</summary>
    public delegate bool AmbientGroundSampler(float worldX, float worldZ, out float worldY);

    /// <summary>
    /// Pure spatial placement: an organic position in a ring around the
    /// player. Area-uniform radial distribution (sqrt of the interpolated
    /// squared radii) so events do not cluster near the inner edge; angle
    /// uniform over the full circle, or over the arc outside the player's
    /// frontal view cone when the profile asks to avoid it.
    /// </summary>
    public static class AmbientEventPlacement
    {
        /// <summary>Half-angle (degrees) of the frontal cone excluded by AvoidPlayerView.</summary>
        public const float ViewConeHalfAngle = 35f;

        public static bool TryResolvePosition(
            Vector3 playerPosition,
            Vector3 playerForward,
            AmbientEventProfile profile,
            System.Random random,
            AmbientGroundSampler groundSampler,
            out Vector3 position)
        {
            position = default;
            if (profile == null || random == null)
            {
                return false;
            }

            Vector2 distance = profile.DistanceRange;
            float minRadius = distance.x;
            float maxRadius = distance.y;
            if (maxRadius <= 0f || maxRadius < minRadius)
            {
                return false;
            }

            // Area-uniform radius: radius = sqrt(lerp(min^2, max^2, u)).
            float u = (float)random.NextDouble();
            float radius = Mathf.Sqrt(Mathf.Lerp(minRadius * minRadius, maxRadius * maxRadius, u));

            float angleDegrees = ResolveAngleDegrees(playerForward, profile.AvoidPlayerView, random);
            float radians = angleDegrees * Mathf.Deg2Rad;

            float x = playerPosition.x + Mathf.Cos(radians) * radius;
            float z = playerPosition.z + Mathf.Sin(radians) * radius;

            float groundY = playerPosition.y;
            if (groundSampler != null && groundSampler(x, z, out float sampledY))
            {
                groundY = sampledY;
            }

            Vector2 heightOffset = profile.HeightOffsetRange;
            float y = groundY + Mathf.Lerp(heightOffset.x, heightOffset.y, (float)random.NextDouble());

            position = new Vector3(x, y, z);
            return true;
        }

        private static float ResolveAngleDegrees(Vector3 playerForward, bool avoidPlayerView, System.Random random)
        {
            var flatForward = new Vector2(playerForward.x, playerForward.z);
            bool hasForward = flatForward.sqrMagnitude > 0.0001f;

            if (!avoidPlayerView || !hasForward)
            {
                return (float)random.NextDouble() * 360f;
            }

            // Uniform over the arc OUTSIDE the frontal cone: offset from the
            // forward direction by [cone, 360 - cone] degrees. Direct sector
            // sampling - no rejection loop needed.
            float forwardDegrees = Mathf.Atan2(flatForward.y, flatForward.x) * Mathf.Rad2Deg;
            float arc = 360f - 2f * ViewConeHalfAngle;
            float offset = ViewConeHalfAngle + (float)random.NextDouble() * arc;
            return forwardDegrees + offset;
        }
    }
}
