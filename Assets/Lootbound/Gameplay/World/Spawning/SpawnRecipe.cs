using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Category of world content a reservation can host.
    /// </summary>
    public enum WorldContentCategory
    {
        Encounter,
        Resource
    }

    /// <summary>
    /// One concrete thing to instantiate as part of a SpawnRecipe.
    /// An encounter produces one entry per enemy; a resource produces one entry
    /// carrying a stack quantity; a landmark produces a single entry.
    /// </summary>
    public sealed class SpawnRecipeEntry
    {
        /// <summary>
        /// World position for this entry (height already sampled from terrain).
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        /// Role of this entry within the recipe.
        /// V1 uses "Member" and "Pickup"; future encounters may add roles such
        /// as leaders or wave indices without changing the shape.
        /// </summary>
        public string Role { get; }

        /// <summary>
        /// Quantity carried by this entry (stack size for pickups, 1 otherwise).
        /// </summary>
        public int Quantity { get; }

        public SpawnRecipeEntry(Vector3 position, string role, int quantity)
        {
            Position = position;
            Role = role;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// Fully resolved, deterministic spawn plan for a single reservation:
    /// which definition occupies it, and exactly what to instantiate where.
    ///
    /// Produced by WorldContentPlanner (pure C#, testable in EditMode).
    /// Consumed by WorldContentSpawner (instantiation only).
    /// Carries the reservation identity and radial context so runtime
    /// instances can keep a persistent link back to the generated layout.
    /// </summary>
    public sealed class SpawnRecipe
    {
        public string ReservationId { get; }
        public string HostNodeId { get; }
        public WorldContentCategory Category { get; }

        /// <summary>
        /// Stable string ID of the selected content definition.
        /// </summary>
        public string DefinitionId { get; }

        /// <summary>
        /// Reservation anchor position (height sampled from terrain).
        /// </summary>
        public Vector3 AnchorPosition { get; }

        public float DistanceFromRefuge { get; }
        public float NormalizedWorldRadius { get; }
        public WorldRing Ring { get; }
        public string RadialPathId { get; }

        public IReadOnlyList<SpawnRecipeEntry> Entries { get; }

        public SpawnRecipe(
            string reservationId,
            string hostNodeId,
            WorldContentCategory category,
            string definitionId,
            Vector3 anchorPosition,
            float distanceFromRefuge,
            float normalizedWorldRadius,
            WorldRing ring,
            string radialPathId,
            IReadOnlyList<SpawnRecipeEntry> entries)
        {
            ReservationId = reservationId;
            HostNodeId = hostNodeId;
            Category = category;
            DefinitionId = definitionId;
            AnchorPosition = anchorPosition;
            DistanceFromRefuge = distanceFromRefuge;
            NormalizedWorldRadius = normalizedWorldRadius;
            Ring = ring;
            RadialPathId = radialPathId;
            Entries = entries;
        }

        public override string ToString()
        {
            return $"SpawnRecipe({Category} {DefinitionId} @ {ReservationId}, {Entries.Count} entries, ring {Ring})";
        }
    }
}
