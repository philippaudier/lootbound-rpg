using Lootbound.World.Processing;

namespace Lootbound.Gameplay.World.Knowledge
{
    /// <summary>
    /// The first named TraversalProfiles - DATA, on the gameplay side, exactly
    /// as PCE 0.2 locked it: the mechanism (TerrainCostField) lives in Terrain
    /// Intelligence and knows no entity; gameplay names the perceptions. Every
    /// value here is provisional tuning, not architecture. A future mover is a
    /// new instance, never a new class.
    /// </summary>
    public static class TraversalProfiles
    {
        /// <summary>
        /// A road-building humanoid: geometry-strict (roads want flat, dry,
        /// even ground), landscape-blind in V1.
        /// </summary>
        public static TraversalProfile HumanRoad() => new TraversalProfile
        {
            BaseCost = 1f,
            SlopeCostPerDegree = 0.09f,
            CliffCost = 150f,
            RoughnessCostPerMetre = 0.8f,
            WaterCost = 30f,
            LandscapeCostMultipliers = null,
        };

        /// <summary>
        /// A mountain traveller: tolerates gradient and rough ground, and is
        /// "at home" up high - passes read as opportunities, plains as detours.
        /// </summary>
        public static TraversalProfile Mountain() => new TraversalProfile
        {
            BaseCost = 1f,
            SlopeCostPerDegree = 0.03f,
            CliffCost = 80f,
            RoughnessCostPerMetre = 0.3f,
            WaterCost = 20f,
            LandscapeCostMultipliers = Multipliers(
                plain: 1.1f, valley: 1.0f, ridge: 0.9f, mountain: 0.9f,
                plateau: 1.0f, pass: 0.6f, basin: 1.0f, cliff: 1.0f),
        };

        /// <summary>
        /// A wild animal: light-footed (roughness barely matters), drawn to
        /// valleys, basins and water, wary of open heights.
        /// </summary>
        public static TraversalProfile Animal() => new TraversalProfile
        {
            BaseCost = 1f,
            SlopeCostPerDegree = 0.04f,
            CliffCost = 120f,
            RoughnessCostPerMetre = 0.2f,
            WaterCost = 8f,
            LandscapeCostMultipliers = Multipliers(
                plain: 0.9f, valley: 0.7f, ridge: 1.2f, mountain: 1.2f,
                plateau: 1.0f, pass: 1.0f, basin: 0.8f, cliff: 1.5f),
        };

        // Named construction so the array stays correct even if the enum order
        // ever changes.
        private static float[] Multipliers(
            float plain, float valley, float ridge, float mountain,
            float plateau, float pass, float basin, float cliff)
        {
            var m = new float[8];
            m[(int)LandscapeType.Plain] = plain;
            m[(int)LandscapeType.Valley] = valley;
            m[(int)LandscapeType.Ridge] = ridge;
            m[(int)LandscapeType.Mountain] = mountain;
            m[(int)LandscapeType.Plateau] = plateau;
            m[(int)LandscapeType.Pass] = pass;
            m[(int)LandscapeType.Basin] = basin;
            m[(int)LandscapeType.Cliff] = cliff;
            return m;
        }
    }
}
