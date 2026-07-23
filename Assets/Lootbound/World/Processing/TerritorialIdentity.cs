namespace Lootbound.World.Processing
{
    /// <summary>
    /// What Territorial Intelligence knows about a place: MEASURES, never a
    /// name (PCE invariant 19). There is deliberately no enum and no label
    /// here - "high valley" or "mountain basin" are judgments a consumer may
    /// derive later; this layer only characterizes. All components are 0..1
    /// and perception-relative (they were measured through a cost view built
    /// from one TraversalProfile).
    /// </summary>
    public readonly struct TerritorialIdentity
    {
        /// <summary>How cheaply this perception moves AROUND here (1 = ideal ground everywhere nearby).</summary>
        public readonly float Accessibility;

        /// <summary>How hard it is to LEAVE here - the cost of the easiest way out (1 = enclosed).</summary>
        public readonly float Isolation;

        /// <summary>How many distinct easy directions exist (1 = open in every direction).</summary>
        public readonly float Connectivity;

        public TerritorialIdentity(float accessibility, float isolation, float connectivity)
        {
            Accessibility = accessibility;
            Isolation = isolation;
            Connectivity = connectivity;
        }
    }
}
