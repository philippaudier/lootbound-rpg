using System;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// How ONE kind of mover PERCEIVES the terrain (PCE 0.3). Deliberately not
    /// "weights": today it weighs, tomorrow it may carry preferences (fear of
    /// exposure, preferred cover, water seeking) without changing the engine.
    ///
    /// This type describes the one who crosses - never the terrain itself, and
    /// never a game entity. The Terrain Cost System knows no Human, Wolf, Deer
    /// or Merchant; gameplay maps its entities to profiles, the engine only
    /// ever sees a profile (PCE invariant 17). There are no specialized
    /// subclasses: a new mover is a new INSTANCE with different values, never a
    /// new class or algorithm.
    ///
    /// Defaults reproduce the historical WorldKnowledgeSettings traversal
    /// values, so a default profile IS the pre-0.3 TraversabilityField.
    /// Treat instances as immutable once composed.
    /// </summary>
    public sealed class TraversalProfile
    {
        /// <summary>Cost of ideal ground. Every cost is at least this.</summary>
        public float BaseCost = 1f;

        /// <summary>Added cost per degree of slope.</summary>
        public float SlopeCostPerDegree = 0.05f;

        /// <summary>Flat penalty when the ground is a cliff.</summary>
        public float CliffCost = 100f;

        /// <summary>Added cost per metre of local roughness.</summary>
        public float RoughnessCostPerMetre = 0.5f;

        /// <summary>Flat penalty for crossing a river cell.</summary>
        public float WaterCost = 25f;

        /// <summary>
        /// Optional perception of landscapes: a multiplier on the total cost,
        /// indexed by <see cref="LandscapeType"/> (length = number of landscape
        /// types, 1 = neutral). Null = this mover does not read landscapes and
        /// the landscape field is never evaluated. This is what lets an animal
        /// prefer valleys or a trail profile favour passes - as DATA, without a
        /// line of new algorithm.
        /// </summary>
        public float[] LandscapeCostMultipliers;

        /// <summary>A profile whose values exactly match the given settings (the historical default).</summary>
        public static TraversalProfile FromSettings(WorldKnowledgeSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new TraversalProfile
            {
                BaseCost = settings.TraversalBaseCost,
                SlopeCostPerDegree = settings.SlopeCostPerDegree,
                CliffCost = settings.CliffCost,
                RoughnessCostPerMetre = settings.RoughnessCostPerMetre,
                WaterCost = settings.WaterCost,
                LandscapeCostMultipliers = null,
            };
        }
    }
}
