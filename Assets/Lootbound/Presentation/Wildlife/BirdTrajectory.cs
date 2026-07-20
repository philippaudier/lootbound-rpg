using UnityEngine;

namespace Lootbound.Presentation.Wildlife
{
    /// <summary>The three light V1 flight shapes.</summary>
    public enum BirdTrajectoryType
    {
        /// <summary>The flock crosses the area from one side to the other.</summary>
        Crossing,

        /// <summary>The flock starts low, climbs and leaves.</summary>
        Rising,

        /// <summary>The flock draws a light arc before leaving the area.</summary>
        Circling
    }

    /// <summary>
    /// A single quadratic Bezier flight path: start, one control point,
    /// end. Deliberately not a generic spline system - three points are
    /// enough for a readable, soft, non-mechanical V1 flight.
    /// </summary>
    public readonly struct BirdTrajectory
    {
        public BirdTrajectoryType Type { get; }
        public Vector3 Start { get; }
        public Vector3 Control { get; }
        public Vector3 End { get; }

        public BirdTrajectory(BirdTrajectoryType type, Vector3 start, Vector3 control, Vector3 end)
        {
            Type = type;
            Start = start;
            Control = control;
            End = end;
        }

        /// <summary>Position on the curve (t clamped to 0..1).</summary>
        public Vector3 Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return u * u * Start + 2f * u * t * Control + t * t * End;
        }

        /// <summary>
        /// Normalized flight direction (t clamped). Never NaN: degenerate
        /// derivatives fall back to the chord, then to world forward.
        /// </summary>
        public Vector3 EvaluateDirection(float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 derivative = 2f * (1f - t) * (Control - Start) + 2f * t * (End - Control);
            if (derivative.sqrMagnitude < 0.000001f)
            {
                derivative = End - Start;
            }

            return derivative.sqrMagnitude < 0.000001f ? Vector3.forward : derivative.normalized;
        }

        /// <summary>Same shape, globally translated (camera-avoidance shift).</summary>
        public BirdTrajectory Translated(Vector3 offset)
        {
            return new BirdTrajectory(Type, Start + offset, Control + offset, End + offset);
        }
    }
}
