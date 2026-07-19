using UnityEngine;

namespace Lootbound.Gameplay.World.Ambience
{
    /// <summary>
    /// Boundary between the ambience core and the actual rendering stack.
    /// Concrete appliers (e.g. the PBSky integration in
    /// Lootbound.Rendering.PBSky) own the Unity/URP details; the gameplay
    /// assembly never references third-party rendering types.
    /// </summary>
    public abstract class WorldAmbienceApplierBase : MonoBehaviour
    {
        /// <summary>
        /// Capture the current visual baseline (existing fog values, light
        /// intensity, ambient intensity). Depth 0 must reproduce it exactly.
        /// Returns false when the applier cannot operate (clean fallback).
        /// </summary>
        public abstract bool TryCaptureBaseline(out WorldAmbienceBaseline baseline);

        /// <summary>Apply the smoothed state to the rendering stack.</summary>
        public abstract void Apply(in WorldAmbienceState state);

        /// <summary>
        /// Restore everything: owned overrides are removed, captured external
        /// values (light, ambient) are written back exactly.
        /// </summary>
        public abstract void Restore();

        /// <summary>
        /// Re-capture the lighting baseline. A future day/night cycle will
        /// call this (or inject a base-light provider) when the sun changes;
        /// V1 captures once and documents the hook.
        /// </summary>
        public abstract void RefreshLightingBaseline();

        /// <summary>One-line status for the F7 panel (volume, fog, light, fallbacks).</summary>
        public abstract string StatusDescription { get; }
    }
}
