using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Progression;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Turns validated world layout reservations into deterministic SpawnRecipes.
    ///
    /// Pure C# - no instantiation happens here, which keeps the whole
    /// selection/validation pipeline testable in EditMode.
    ///
    /// Determinism: every decision for a reservation is drawn from a
    /// System.Random seeded by (worldSeed, ReservationId). Because the seed
    /// derives from the reservation's stable ID rather than its position in a
    /// collection, results are independent of iteration order and never touch
    /// UnityEngine.Random.
    /// </summary>
    public static class WorldContentPlanner
    {
        private const string ROLE_MEMBER = "Member";
        private const string ROLE_PICKUP = "Pickup";
        private const string ROLE_LANDMARK = "Landmark";

        /// <summary>
        /// Plan all reservations of a validated layout.
        /// Null reservation lists are treated as empty; a null registry disables its category.
        /// </summary>
        public static WorldContentPlan Plan(
            int worldSeed,
            IReadOnlyList<EncounterReservation> encounterReservations,
            IReadOnlyList<ResourceReservation> resourceReservations,
            IReadOnlyList<LandmarkReservation> landmarkReservations,
            EncounterRegistry encounterRegistry,
            ResourceSpawnRegistry resourceRegistry,
            LandmarkRegistry landmarkRegistry,
            ITerrainSampler sampler,
            WorldContentPlannerSettings settings = null,
            WorldProgression progression = null)
        {
            settings = settings ?? new WorldContentPlannerSettings();

            var recipes = new List<SpawnRecipe>();
            var rejections = new List<SpawnRejection>();
            int totalReservations = 0;

            if (encounterReservations != null)
            {
                foreach (var reservation in encounterReservations)
                {
                    totalReservations++;
                    PlanEncounter(worldSeed, reservation, encounterRegistry, sampler, settings, progression, recipes, rejections);
                }
            }

            if (resourceReservations != null)
            {
                foreach (var reservation in resourceReservations)
                {
                    totalReservations++;
                    PlanResource(worldSeed, reservation, resourceRegistry, sampler, settings, progression, recipes, rejections);
                }
            }

            if (landmarkReservations != null)
            {
                foreach (var reservation in landmarkReservations)
                {
                    totalReservations++;
                    PlanLandmark(worldSeed, reservation, landmarkRegistry, sampler, settings, progression, recipes, rejections);
                }
            }

            return new WorldContentPlan(recipes, rejections, totalReservations);
        }

        /// <summary>
        /// Global depth (Depth01) at a reservation. The progression authority
        /// is preferred; without it, the reservation's recorded normalized
        /// radius (produced by the same ring evaluator at layout time) is
        /// clamped - never a local distance recomputation.
        /// </summary>
        private static float DepthAt(float distanceFromRefuge, float normalizedWorldRadius, WorldProgression progression)
        {
            return progression != null
                ? progression.GetContextFromDistance(distanceFromRefuge).Depth01
                : Mathf.Clamp01(normalizedWorldRadius);
        }

        /// <summary>
        /// Deterministic weighted pick. Consumes exactly one random draw.
        /// </summary>
        private static T SelectWeighted<T>(List<(T item, float weight)> candidates, double totalWeight, System.Random random)
        {
            double roll = random.NextDouble() * totalWeight;
            double accumulated = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                accumulated += candidates[i].weight;
                if (roll < accumulated)
                {
                    return candidates[i].item;
                }
            }

            return candidates[candidates.Count - 1].item;
        }

        #region Encounters

        private static void PlanEncounter(
            int worldSeed,
            EncounterReservation reservation,
            EncounterRegistry registry,
            ITerrainSampler sampler,
            WorldContentPlannerSettings settings,
            WorldProgression progression,
            List<SpawnRecipe> recipes,
            List<SpawnRejection> rejections)
        {
            if (!settings.EncountersEnabled || registry == null)
            {
                rejections.Add(new SpawnRejection(reservation.ReservationId, WorldContentCategory.Encounter,
                    SpawnRejectionReason.CategoryDisabled));
                return;
            }

            // Hard rule: the Refuge ring never hosts hostile encounters,
            // regardless of what any definition claims to allow.
            if (reservation.Ring == WorldRing.Refuge)
            {
                rejections.Add(new SpawnRejection(reservation.ReservationId, WorldContentCategory.Encounter,
                    SpawnRejectionReason.RefugeExclusion));
                return;
            }

            if (!ValidateAnchor(reservation.ReservationId, WorldContentCategory.Encounter,
                    reservation.Position, sampler, settings, rejections))
            {
                return;
            }

            float depth = DepthAt(reservation.DistanceFromRefuge, reservation.NormalizedWorldRadius, progression);
            var compatible = new List<(EncounterDefinition item, float weight)>();
            double totalWeight = 0;
            foreach (var definition in registry.Definitions)
            {
                if (definition != null &&
                    WorldContentCompatibility.Evaluate(definition, reservation.Ring, depth, out float weight, out _))
                {
                    compatible.Add((definition, weight));
                    totalWeight += weight;
                }
            }

            if (compatible.Count == 0)
            {
                rejections.Add(new SpawnRejection(reservation.ReservationId, WorldContentCategory.Encounter,
                    SpawnRejectionReason.NoCompatibleDefinition, $"ring {reservation.Ring}, depth {depth:F2}"));
                return;
            }

            var random = CreateReservationRandom(worldSeed, reservation.ReservationId);
            var selected = SelectWeighted(compatible, totalWeight, random);

            if (selected.EnemyPrefab == null)
            {
                rejections.Add(new SpawnRejection(reservation.ReservationId, WorldContentCategory.Encounter,
                    SpawnRejectionReason.MissingPrefab, selected.EncounterId));
                return;
            }

            int memberCount = random.Next(selected.MinimumEnemyCount, selected.MaximumEnemyCount + 1);
            var entries = new List<SpawnRecipeEntry>(memberCount);
            Vector3 anchor = GroundedPosition(reservation.Position, sampler);

            for (int member = 0; member < memberCount; member++)
            {
                Vector3 position = FindMemberPosition(anchor, selected.SpawnSpreadRadius, random, sampler, settings);
                entries.Add(new SpawnRecipeEntry(position, ROLE_MEMBER, 1));
            }

            recipes.Add(CreateRecipe(reservation.ReservationId, reservation.HostNodeId,
                WorldContentCategory.Encounter, selected.EncounterId, anchor,
                reservation.DistanceFromRefuge, reservation.NormalizedWorldRadius,
                reservation.Ring, reservation.RadialPathId, entries));
        }

        private static Vector3 FindMemberPosition(
            Vector3 anchor,
            float spreadRadius,
            System.Random random,
            ITerrainSampler sampler,
            WorldContentPlannerSettings settings)
        {
            for (int attempt = 0; attempt < settings.PlacementAttemptsPerEntry; attempt++)
            {
                float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
                float distance = (float)(random.NextDouble()) * spreadRadius;
                float x = anchor.x + Mathf.Cos(angle) * distance;
                float z = anchor.z + Mathf.Sin(angle) * distance;

                if (!sampler.IsWithinBounds(x, z)) continue;
                if (sampler.SampleSlope(x, z) > settings.MaxPlacementSlope) continue;

                return new Vector3(x, sampler.SampleHeight(x, z), z);
            }

            // Anchor is already validated; use it as the safe fallback.
            return anchor;
        }

        #endregion

        #region Resources

        private static void PlanResource(
            int worldSeed,
            ResourceReservation reservation,
            ResourceSpawnRegistry registry,
            ITerrainSampler sampler,
            WorldContentPlannerSettings settings,
            WorldProgression progression,
            List<SpawnRecipe> recipes,
            List<SpawnRejection> rejections)
        {
            if (!settings.ResourcesEnabled || registry == null)
            {
                rejections.Add(new SpawnRejection(reservation.ReservationId, WorldContentCategory.Resource,
                    SpawnRejectionReason.CategoryDisabled));
                return;
            }

            if (!ValidateAnchor(reservation.ReservationId, WorldContentCategory.Resource,
                    reservation.Position, sampler, settings, rejections))
            {
                return;
            }

            float depth = DepthAt(reservation.DistanceFromRefuge, reservation.NormalizedWorldRadius, progression);
            var compatible = new List<(ResourceSpawnDefinition item, float weight)>();
            double totalWeight = 0;
            foreach (var definition in registry.Definitions)
            {
                if (definition != null &&
                    WorldContentCompatibility.Evaluate(definition, reservation.Ring, depth, out float weight, out _))
                {
                    compatible.Add((definition, weight));
                    totalWeight += weight;
                }
            }

            if (compatible.Count == 0)
            {
                rejections.Add(new SpawnRejection(reservation.ReservationId, WorldContentCategory.Resource,
                    SpawnRejectionReason.NoCompatibleDefinition, $"ring {reservation.Ring}, depth {depth:F2}"));
                return;
            }

            var random = CreateReservationRandom(worldSeed, reservation.ReservationId);
            var selected = SelectWeighted(compatible, totalWeight, random);

            if (selected.Item == null)
            {
                rejections.Add(new SpawnRejection(reservation.ReservationId, WorldContentCategory.Resource,
                    SpawnRejectionReason.MissingItem, selected.ResourceId));
                return;
            }

            int quantity = random.Next(selected.MinimumQuantity, selected.MaximumQuantity + 1);
            Vector3 anchor = GroundedPosition(reservation.Position, sampler);
            var entries = new List<SpawnRecipeEntry>
            {
                new SpawnRecipeEntry(anchor, ROLE_PICKUP, quantity)
            };

            recipes.Add(CreateRecipe(reservation.ReservationId, reservation.HostNodeId,
                WorldContentCategory.Resource, selected.ResourceId, anchor,
                reservation.DistanceFromRefuge, reservation.NormalizedWorldRadius,
                reservation.Ring, reservation.RadialPathId, entries));
        }

        #endregion

        #region Landmarks

        private static void PlanLandmark(
            int worldSeed,
            LandmarkReservation reservation,
            LandmarkRegistry registry,
            ITerrainSampler sampler,
            WorldContentPlannerSettings settings,
            WorldProgression progression,
            List<SpawnRecipe> recipes,
            List<SpawnRejection> rejections)
        {
            if (!settings.LandmarksEnabled || registry == null)
            {
                rejections.Add(new SpawnRejection(reservation.ReservationId, WorldContentCategory.Landmark,
                    SpawnRejectionReason.CategoryDisabled));
                return;
            }

            if (!ValidateAnchor(reservation.ReservationId, WorldContentCategory.Landmark,
                    reservation.Position, sampler, settings, rejections))
            {
                return;
            }

            float depth = DepthAt(reservation.DistanceFromRefuge, reservation.NormalizedWorldRadius, progression);
            var compatible = new List<(LandmarkDefinition item, float weight)>();
            double totalWeight = 0;
            foreach (var definition in registry.Definitions)
            {
                if (definition != null &&
                    WorldContentCompatibility.Evaluate(definition, reservation.Ring, depth, out float weight, out _))
                {
                    compatible.Add((definition, weight));
                    totalWeight += weight;
                }
            }

            if (compatible.Count == 0)
            {
                rejections.Add(new SpawnRejection(reservation.ReservationId, WorldContentCategory.Landmark,
                    SpawnRejectionReason.NoCompatibleDefinition, $"ring {reservation.Ring}, depth {depth:F2}"));
                return;
            }

            var random = CreateReservationRandom(worldSeed, reservation.ReservationId);
            var selected = SelectWeighted(compatible, totalWeight, random);

            // A landmark definition without prefab is valid: the spawner
            // substitutes a clearly named placeholder primitive.
            Vector3 anchor = GroundedPosition(reservation.Position, sampler);
            var entries = new List<SpawnRecipeEntry>
            {
                new SpawnRecipeEntry(anchor, ROLE_LANDMARK, 1)
            };

            recipes.Add(CreateRecipe(reservation.ReservationId, reservation.HostNodeId,
                WorldContentCategory.Landmark, selected.LandmarkId, anchor,
                reservation.DistanceFromRefuge, reservation.NormalizedWorldRadius,
                reservation.Ring, reservation.RadialPathId, entries));
        }

        #endregion

        #region Shared helpers

        private static bool ValidateAnchor(
            string reservationId,
            WorldContentCategory category,
            Vector3 position,
            ITerrainSampler sampler,
            WorldContentPlannerSettings settings,
            List<SpawnRejection> rejections)
        {
            if (!sampler.IsWithinBounds(position.x, position.z))
            {
                rejections.Add(new SpawnRejection(reservationId, category,
                    SpawnRejectionReason.OutOfTerrainBounds));
                return false;
            }

            float slope = sampler.SampleSlope(position.x, position.z);
            if (slope > settings.MaxPlacementSlope)
            {
                rejections.Add(new SpawnRejection(reservationId, category,
                    SpawnRejectionReason.SlopeTooSteep, $"{slope:F1} deg > {settings.MaxPlacementSlope:F1} deg"));
                return false;
            }

            return true;
        }

        private static Vector3 GroundedPosition(Vector3 position, ITerrainSampler sampler)
        {
            return new Vector3(position.x, sampler.SampleHeight(position.x, position.z), position.z);
        }

        private static SpawnRecipe CreateRecipe(
            string reservationId,
            string hostNodeId,
            WorldContentCategory category,
            string definitionId,
            Vector3 anchor,
            float distanceFromRefuge,
            float normalizedWorldRadius,
            WorldRing ring,
            string radialPathId,
            List<SpawnRecipeEntry> entries)
        {
            return new SpawnRecipe(reservationId, hostNodeId, category, definitionId, anchor,
                distanceFromRefuge, normalizedWorldRadius, ring, radialPathId, entries);
        }

        /// <summary>
        /// Deterministic per-reservation random source.
        /// FNV-1a over the world seed and the reservation's stable ID:
        /// stable across processes (unlike string.GetHashCode) and independent
        /// of collection iteration order.
        /// </summary>
        private static System.Random CreateReservationRandom(int worldSeed, string reservationId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)worldSeed) * 16777619u;
                if (reservationId != null)
                {
                    foreach (char c in reservationId)
                    {
                        hash = (hash ^ c) * 16777619u;
                    }
                }
                return new System.Random((int)hash);
            }
        }

        #endregion
    }
}
