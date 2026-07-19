using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Population
{
    /// <summary>
    /// Cell math for the ambient population grid. Cells are expressed
    /// RELATIVE to the logical WorldDisc center (the Refuge position) - an
    /// explicit, declared dependency: moving the disc or hosting several
    /// world instances can never silently reshuffle the grid.
    /// </summary>
    public static class AmbientPopulationCells
    {
        public static Vector2Int WorldToCell(Vector3 worldPosition, Vector3 discCenter, float cellSize)
        {
            float localX = worldPosition.x - discCenter.x;
            float localZ = worldPosition.z - discCenter.z;
            return new Vector2Int(
                Mathf.FloorToInt(localX / cellSize),
                Mathf.FloorToInt(localZ / cellSize));
        }

        /// <summary>World-space center of a cell (XZ; Y is the disc center's).</summary>
        public static Vector3 CellCenter(Vector2Int cell, Vector3 discCenter, float cellSize)
        {
            return new Vector3(
                discCenter.x + (cell.x + 0.5f) * cellSize,
                discCenter.y,
                discCenter.z + (cell.y + 0.5f) * cellSize);
        }

        /// <summary>World-space minimum corner of a cell.</summary>
        public static Vector3 CellMinCorner(Vector2Int cell, Vector3 discCenter, float cellSize)
        {
            return new Vector3(
                discCenter.x + cell.x * cellSize,
                discCenter.y,
                discCenter.z + cell.y * cellSize);
        }

        /// <summary>All cells whose bounds intersect a radius around a position.</summary>
        public static void CollectCellsInRadius(
            Vector3 worldPosition, float radius, Vector3 discCenter, float cellSize, List<Vector2Int> results)
        {
            results.Clear();
            Vector2Int min = WorldToCell(worldPosition + new Vector3(-radius, 0f, -radius), discCenter, cellSize);
            Vector2Int max = WorldToCell(worldPosition + new Vector3(radius, 0f, radius), discCenter, cellSize);

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    results.Add(new Vector2Int(x, y));
                }
            }
        }
    }

    /// <summary>
    /// Deterministic ambient identity and RNG derivation. IDs are explicitly
    /// namespaced ("ambient_v{version}_...") so they can never collide
    /// logically with authored reservation IDs sharing the same integers.
    /// </summary>
    public static class AmbientPopulationIds
    {
        /// <summary>Bumping this reshuffles every ambient intention (never the authored content).</summary>
        public const int PopulationGenerationVersion = 1;

        public static string PlanId(Vector2Int cell, int anchorIndex)
        {
            return $"ambient_v{PopulationGenerationVersion}_{cell.x}_{cell.y}_a{anchorIndex}";
        }

        public static string MemberId(string planId, int memberIndex)
        {
            return $"{planId}_m{memberIndex}";
        }

        /// <summary>Stable FNV-1a seed for a cell.</summary>
        public static int CellSeed(int worldSeed, Vector2Int cell)
        {
            unchecked
            {
                uint hash = 2166136261u;
                void Mix(int value) => hash = (hash ^ (uint)value) * 16777619u;
                foreach (char c in "Ambient") Mix(c);
                Mix(PopulationGenerationVersion);
                Mix(worldSeed);
                Mix(cell.x);
                Mix(cell.y);
                return (int)hash;
            }
        }

        /// <summary>Stable seed for one plan (anchor) inside a cell.</summary>
        public static int PlanSeed(int worldSeed, Vector2Int cell, int anchorIndex)
        {
            unchecked
            {
                uint hash = (uint)CellSeed(worldSeed, cell);
                hash = (hash ^ (uint)anchorIndex) * 16777619u;
                return (int)hash;
            }
        }
    }

    /// <summary>
    /// The intention of one ambient presence: what should live here.
    /// Produced deterministically by the planner; materialized (or not) by
    /// the runtime. Carries several stable candidate positions so a single
    /// unlucky NavMesh point never condemns the whole intention.
    /// </summary>
    public readonly struct AmbientPopulationPlan
    {
        public string PlanId { get; }
        public string PopulationId { get; }
        public Vector2Int CellCoordinate { get; }
        public int AnchorIndex { get; }
        public int GroupSize { get; }
        public int StableSeed { get; }
        public IReadOnlyList<Vector3> CandidatePositions { get; }

        public AmbientPopulationPlan(
            string planId, string populationId, Vector2Int cellCoordinate, int anchorIndex,
            int groupSize, int stableSeed, IReadOnlyList<Vector3> candidatePositions)
        {
            PlanId = planId;
            PopulationId = populationId;
            CellCoordinate = cellCoordinate;
            AnchorIndex = anchorIndex;
            GroupSize = groupSize;
            StableSeed = stableSeed;
            CandidatePositions = candidatePositions;
        }

        public override string ToString() =>
            $"[{PlanId}] {PopulationId} x{GroupSize} ({CandidatePositions?.Count ?? 0} candidates)";
    }

    /// <summary>
    /// Why a spawn was rejected. Structural reasons invalidate a candidate
    /// for this generation; transient reasons only mean "not now" and must
    /// never exhaust a cell, drop a plan or trigger a re-roll.
    /// </summary>
    public enum AmbientSpawnRejectionReason
    {
        None,

        // Structural - permanently invalid for this generation
        OutsideWorldDisc,
        RingIncompatible,
        SlopeInvalid,
        NoNavMesh,
        TooCloseToRefuge,
        TooCloseToAuthoredContent,

        // Transient - retry later, same intention
        PlayerTooClose,
        VisibleWithinProtectionDistance,
        GlobalBudgetReached,
        DefinitionBudgetReached,
        CellBudgetReached,
        NeighborTooClose,
        RuntimeNotReady
    }

    public enum AmbientSpawnRejectionKind
    {
        None,
        Structural,
        Transient
    }

    public static class AmbientSpawnRejection
    {
        public static AmbientSpawnRejectionKind KindOf(AmbientSpawnRejectionReason reason)
        {
            switch (reason)
            {
                case AmbientSpawnRejectionReason.None:
                    return AmbientSpawnRejectionKind.None;

                case AmbientSpawnRejectionReason.OutsideWorldDisc:
                case AmbientSpawnRejectionReason.RingIncompatible:
                case AmbientSpawnRejectionReason.SlopeInvalid:
                case AmbientSpawnRejectionReason.NoNavMesh:
                case AmbientSpawnRejectionReason.TooCloseToRefuge:
                case AmbientSpawnRejectionReason.TooCloseToAuthoredContent:
                    return AmbientSpawnRejectionKind.Structural;
                // (all remaining reasons, including VisibleWithinProtectionDistance,
                // fall through to Transient below)

                default:
                    return AmbientSpawnRejectionKind.Transient;
            }
        }

        public static bool IsRetryable(AmbientSpawnRejectionReason reason)
        {
            return KindOf(reason) == AmbientSpawnRejectionKind.Transient;
        }
    }

    /// <summary>
    /// Outcome of validating one plan (best candidate retained).
    /// </summary>
    public readonly struct AmbientSpawnValidationResult
    {
        public bool IsValid { get; }
        public AmbientSpawnRejectionReason RejectionReason { get; }
        public AmbientSpawnRejectionKind RejectionKind => AmbientSpawnRejection.KindOf(RejectionReason);
        public Vector3 RequestedPosition { get; }
        public Vector3 ResolvedPosition { get; }

        private AmbientSpawnValidationResult(bool isValid, AmbientSpawnRejectionReason reason,
            Vector3 requested, Vector3 resolved)
        {
            IsValid = isValid;
            RejectionReason = reason;
            RequestedPosition = requested;
            ResolvedPosition = resolved;
        }

        public static AmbientSpawnValidationResult Valid(Vector3 requested, Vector3 resolved) =>
            new AmbientSpawnValidationResult(true, AmbientSpawnRejectionReason.None, requested, resolved);

        public static AmbientSpawnValidationResult Rejected(AmbientSpawnRejectionReason reason, Vector3 requested) =>
            new AmbientSpawnValidationResult(false, reason, requested, requested);
    }

    /// <summary>
    /// Minimal view of a living ambient instance (position readable without
    /// Unity lookups; test fakes implement it trivially).
    /// </summary>
    public interface IAmbientInstance
    {
        string MemberId { get; }
        string PopulationId { get; }
        Vector2Int Cell { get; }
        Vector3 Position { get; }
    }

    /// <summary>
    /// What the world remembers about a despawned creature: identity and
    /// health. Streaming must never heal an enemy for free; it respawns at
    /// its resolved anchor (never mid-chase) with its previous health.
    /// </summary>
    public readonly struct AmbientInstanceSnapshot
    {
        public string MemberId { get; }
        public float NormalizedHealth { get; }
        public bool WasEngaged { get; }

        public AmbientInstanceSnapshot(string memberId, float normalizedHealth, bool wasEngaged)
        {
            MemberId = memberId;
            NormalizedHealth = normalizedHealth;
            WasEngaged = wasEngaged;
        }
    }
}
