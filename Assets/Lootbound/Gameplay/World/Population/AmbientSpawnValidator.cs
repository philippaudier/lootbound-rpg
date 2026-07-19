using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Progression;
using Lootbound.Gameplay.Combat;

namespace Lootbound.Gameplay.World.Population
{
    /// <summary>
    /// Everything the validator needs to answer "is this possible here, NOW?".
    /// Dependencies are injected (samplers, delegates, positions) so every
    /// rule is testable in EditMode with fakes.
    /// </summary>
    public sealed class AmbientSpawnValidationContext
    {
        public WorldProgression Progression;
        public ITerrainSampler TerrainSampler;
        public NavigationSampleDelegate SampleNavMesh;
        public AmbientPopulationRegistry Registry;
        public AmbientPopulationConfig Config;

        public Vector3? PlayerPosition;

        /// <summary>Camera frustum planes (null = no frustum rejection).</summary>
        public Plane[] FrustumPlanes;

        // Authored reservation anchor positions, by category.
        public IReadOnlyList<Vector3> EncounterPositions;
        public IReadOnlyList<Vector3> LandmarkPositions;
        public IReadOnlyList<Vector3> ResourcePositions;
    }

    /// <summary>
    /// Answers "is this plan possible here, NOW?". Candidates are tried in
    /// their stable order. Structural rejections permanently invalidate a
    /// candidate for this generation; transient rejections only mean "not
    /// now" and never exhaust a plan. The final result reports:
    /// valid (with resolved position), or the retained rejection - transient
    /// if ANY candidate was only transiently blocked, structural only when
    /// every candidate is structurally dead.
    /// </summary>
    public static class AmbientSpawnValidator
    {
        public static AmbientSpawnValidationResult Validate(
            in AmbientPopulationPlan plan,
            AmbientPopulationDefinition definition,
            AmbientSpawnValidationContext context)
        {
            AmbientSpawnRejectionReason firstTransient = AmbientSpawnRejectionReason.None;
            AmbientSpawnRejectionReason lastStructural = AmbientSpawnRejectionReason.None;
            Vector3 firstRejected = plan.CandidatePositions.Count > 0 ? plan.CandidatePositions[0] : Vector3.zero;

            foreach (var candidate in plan.CandidatePositions)
            {
                var reason = ValidateCandidate(candidate, definition, context, out Vector3 resolved);
                if (reason == AmbientSpawnRejectionReason.None)
                {
                    return AmbientSpawnValidationResult.Valid(candidate, resolved);
                }

                if (AmbientSpawnRejection.IsRetryable(reason))
                {
                    if (firstTransient == AmbientSpawnRejectionReason.None)
                    {
                        firstTransient = reason;
                        firstRejected = candidate;
                    }
                }
                else
                {
                    lastStructural = reason;
                }
            }

            // Any transient blocker means the intention stays alive: retry later.
            if (firstTransient != AmbientSpawnRejectionReason.None)
            {
                return AmbientSpawnValidationResult.Rejected(firstTransient, firstRejected);
            }

            return AmbientSpawnValidationResult.Rejected(lastStructural, firstRejected);
        }

        private static AmbientSpawnRejectionReason ValidateCandidate(
            Vector3 candidate,
            AmbientPopulationDefinition definition,
            AmbientSpawnValidationContext context,
            out Vector3 resolved)
        {
            resolved = candidate;
            var config = context.Config;

            // --- Structural: the world itself ---
            var ringContext = context.Progression.GetContext(candidate);

            if (!ringContext.IsInsideWorldDisc)
            {
                return AmbientSpawnRejectionReason.OutsideWorldDisc;
            }

            if (ringContext.Ring < definition.MinimumRing || ringContext.Ring > definition.MaximumRing)
            {
                return AmbientSpawnRejectionReason.RingIncompatible;
            }

            float refugeBuffer = Mathf.Max(config.MinimumDistanceFromRefuge, definition.MinimumDistanceFromRefuge);
            if (ringContext.DistanceFromRefuge < refugeBuffer)
            {
                return AmbientSpawnRejectionReason.TooCloseToRefuge;
            }

            if (context.TerrainSampler != null)
            {
                if (!context.TerrainSampler.IsWithinBounds(candidate.x, candidate.z))
                {
                    return AmbientSpawnRejectionReason.OutsideWorldDisc;
                }

                if (context.TerrainSampler.SampleSlope(candidate.x, candidate.z) > definition.MaximumSlope)
                {
                    return AmbientSpawnRejectionReason.SlopeInvalid;
                }
            }

            if (IsTooCloseToAuthored(candidate, definition, context))
            {
                return AmbientSpawnRejectionReason.TooCloseToAuthoredContent;
            }

            // NavMesh projection (structural: unreachable ground stays unreachable)
            Vector3 grounded = candidate;
            if (context.TerrainSampler != null)
            {
                grounded.y = context.TerrainSampler.SampleHeight(candidate.x, candidate.z);
            }

            if (context.SampleNavMesh == null || !context.SampleNavMesh(grounded, 4f, out resolved))
            {
                return AmbientSpawnRejectionReason.NoNavMesh;
            }

            // --- Transient: the current moment ---
            if (context.PlayerPosition.HasValue)
            {
                if (Vector3.Distance(resolved, context.PlayerPosition.Value) < config.MinimumDistanceFromPlayer)
                {
                    return AmbientSpawnRejectionReason.PlayerTooClose;
                }
            }

            if (config.RejectInsideCameraFrustum && context.FrustumPlanes != null &&
                IsInsideFrustum(resolved, context.FrustumPlanes))
            {
                return AmbientSpawnRejectionReason.InsideCameraFrustum;
            }

            if (context.Registry != null &&
                context.Registry.IsAnyAliveWithin(resolved, config.MinimumDistanceBetweenIndividuals,
                    context.Progression.RefugePosition, config.CellSize))
            {
                return AmbientSpawnRejectionReason.NeighborTooClose;
            }

            return AmbientSpawnRejectionReason.None;
        }

        private static bool IsTooCloseToAuthored(
            Vector3 candidate, AmbientPopulationDefinition definition, AmbientSpawnValidationContext context)
        {
            float exclusion = definition.MinimumDistanceFromAuthoredContent;
            if (exclusion <= 0f)
            {
                return false;
            }

            float exclusionSqr = exclusion * exclusion;

            if (definition.ExcludeNearEncounters && IsNearAny(candidate, context.EncounterPositions, exclusionSqr)) return true;
            if (definition.ExcludeNearLandmarks && IsNearAny(candidate, context.LandmarkPositions, exclusionSqr)) return true;
            if (definition.ExcludeNearResources && IsNearAny(candidate, context.ResourcePositions, exclusionSqr)) return true;

            return false;
        }

        private static bool IsNearAny(Vector3 position, IReadOnlyList<Vector3> points, float distanceSqr)
        {
            if (points == null) return false;

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 delta = points[i] - position;
                delta.y = 0f;
                if (delta.sqrMagnitude < distanceSqr)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A point is inside the frustum when in front of all six planes.</summary>
        public static bool IsInsideFrustum(Vector3 point, Plane[] planes)
        {
            for (int i = 0; i < planes.Length; i++)
            {
                if (planes[i].GetDistanceToPoint(point) < 0f)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
