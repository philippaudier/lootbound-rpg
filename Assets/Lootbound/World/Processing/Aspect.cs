namespace Lootbound.World.Processing
{
    /// <summary>
    /// Cardinal classification of a slope's facing direction - a debug/consumer
    /// aid over the raw <see cref="ExposureField"/> angle (which keeps the full
    /// precision). Flat = no meaningful aspect (near-zero slope).
    /// </summary>
    public enum Aspect
    {
        Flat,
        North,
        East,
        South,
        West
    }

    public static class AspectClassifier
    {
        /// <summary>Classify an aspect bearing in degrees (0 = North, clockwise). Negative = flat.</summary>
        public static Aspect FromBearing(float bearingDegrees)
        {
            if (bearingDegrees < 0f) return Aspect.Flat;
            float b = bearingDegrees % 360f;
            if (b < 45f || b >= 315f) return Aspect.North;
            if (b < 135f) return Aspect.East;
            if (b < 225f) return Aspect.South;
            return Aspect.West;
        }
    }
}
