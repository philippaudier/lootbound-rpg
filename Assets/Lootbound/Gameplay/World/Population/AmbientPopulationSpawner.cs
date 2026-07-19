using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.Combat;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Gameplay.World.Population
{
    /// <summary>
    /// A living ambient creature: the registry view plus everything needed
    /// to stream it out cleanly (health for snapshots, event unsubscription).
    /// </summary>
    public sealed class AmbientPopulationInstance : IAmbientInstance
    {
        public string MemberId { get; }
        public string PopulationId { get; }
        public Vector2Int Cell { get; }
        public GameObject GameObject { get; }
        public EnemyHealth Health { get; }
        public EnemyBrain Brain { get; }
        public Vector3 AnchorPosition { get; }
        public float SpawnedAt { get; }

        /// <summary>Time at which the despawn grace started (negative = not started).</summary>
        public float DespawnGraceStartedAt = -1f;

        private System.Action diedHandler;

        public Vector3 Position => GameObject != null ? GameObject.transform.position : AnchorPosition;

        public AmbientPopulationInstance(
            string memberId, string populationId, Vector2Int cell, GameObject gameObject,
            Vector3 anchorPosition, float spawnedAt)
        {
            MemberId = memberId;
            PopulationId = populationId;
            Cell = cell;
            GameObject = gameObject;
            AnchorPosition = anchorPosition;
            SpawnedAt = spawnedAt;
            Health = gameObject != null ? gameObject.GetComponent<EnemyHealth>() : null;
            Brain = gameObject != null ? gameObject.GetComponent<EnemyBrain>() : null;
        }

        public void SubscribeDeath(System.Action handler)
        {
            if (Health == null) return;
            diedHandler = handler;
            Health.OnDied += diedHandler;
        }

        public void UnsubscribeDeath()
        {
            if (Health != null && diedHandler != null)
            {
                Health.OnDied -= diedHandler;
            }
            diedHandler = null;
        }
    }

    /// <summary>
    /// Materializes validated plans. Members are placed deterministically
    /// around the resolved anchor (plan.StableSeed), warped onto the NavMesh
    /// so EnemyBrain captures its HomePosition from the final placement -
    /// exactly like authored enemies. Streaming reload restores the
    /// remembered health (never a free heal) and always respawns at the
    /// resolved anchor, never at a mid-chase position.
    /// </summary>
    public sealed class AmbientPopulationSpawner
    {
        private const string LogCategory = "AmbientPopulation";

        private readonly AmbientPopulationRegistry registry;
        private readonly Transform root;
        private readonly int worldSeed;

        public AmbientPopulationSpawner(AmbientPopulationRegistry registry, Transform root, int worldSeed)
        {
            this.registry = registry;
            this.root = root;
            this.worldSeed = worldSeed;
        }

        /// <summary>
        /// Spawn every not-yet-alive, not-dead member of the plan around the
        /// resolved anchor. Returns the spawned instances.
        /// </summary>
        public List<AmbientPopulationInstance> SpawnPlan(
            AmbientPopulationRegistry.PlanRecord record,
            AmbientPopulationDefinition definition,
            Vector3 resolvedAnchor,
            NavigationSampleDelegate sampleNavMesh,
            float now,
            System.Action<AmbientPopulationInstance> onMemberDied)
        {
            var spawned = new List<AmbientPopulationInstance>();
            if (definition.Prefab == null)
            {
                LootboundLog.Warning(LogCategory, $"{record.Plan.PlanId}: definition has no prefab");
                return spawned;
            }

            // Deterministic member offsets: same plan, same layout around the anchor.
            var memberRandom = new System.Random(record.Plan.StableSeed);

            for (int member = 0; member < record.Plan.GroupSize; member++)
            {
                // Fixed draw count per member, consumed even for skipped ones,
                // so offsets never shift when a member is dead or alive.
                float angle = (float)(memberRandom.NextDouble() * Mathf.PI * 2.0);
                float distance = (float)memberRandom.NextDouble() * definition.GroupRadius;
                float yaw = (float)(memberRandom.NextDouble() * 360.0);

                string memberId = AmbientPopulationIds.MemberId(record.Plan.PlanId, member);
                if (registry.IsMemberDead(record, memberId) || registry.IsMemberAlive(memberId))
                {
                    continue;
                }

                Vector3 desired = resolvedAnchor + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                if (!sampleNavMesh(desired, 4f, out Vector3 position))
                {
                    position = resolvedAnchor;
                }

                var creature = Object.Instantiate(definition.Prefab, position, Quaternion.Euler(0f, yaw, 0f), root);
                creature.name = memberId;

                var identity = creature.GetComponent<WorldContentIdentity>();
                if (identity == null)
                {
                    identity = creature.AddComponent<WorldContentIdentity>();
                }
                identity.InitializeAmbient(memberId, definition.PopulationId, worldSeed, record.Plan.CellCoordinate, member);

                var agent = creature.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = true;
                    agent.Warp(position);
                }

                var instance = new AmbientPopulationInstance(
                    memberId, definition.PopulationId, record.Plan.CellCoordinate, creature, resolvedAnchor, now);

                // Streaming memory: a previously wounded creature comes back wounded.
                if (registry.TryGetSnapshot(memberId, out var snapshot) && instance.Health != null)
                {
                    instance.Health.RestoreNormalizedHealth(snapshot.NormalizedHealth);
                }

                instance.SubscribeDeath(() => onMemberDied(instance));
                registry.RegisterSpawned(record, instance);
                spawned.Add(instance);
            }

            return spawned;
        }

        /// <summary>
        /// Streaming despawn: snapshot health, unsubscribe, destroy. The plan
        /// stays pending - the same presence can return at its anchor.
        /// </summary>
        public void Despawn(AmbientPopulationRegistry.PlanRecord record, AmbientPopulationInstance instance)
        {
            float normalizedHealth = instance.Health != null ? instance.Health.NormalizedHealth : 1f;
            bool wasEngaged = instance.Brain != null && instance.Brain.TargetVisible;

            instance.UnsubscribeDeath();
            registry.UnregisterDespawned(record, instance,
                new AmbientInstanceSnapshot(instance.MemberId, normalizedHealth, wasEngaged));

            if (instance.GameObject != null)
            {
                Object.Destroy(instance.GameObject);
            }
        }

        /// <summary>
        /// Purge one instance without keeping any memory (world regeneration).
        /// </summary>
        public static void DestroyForPurge(AmbientPopulationInstance instance)
        {
            instance.UnsubscribeDeath();
            if (instance.GameObject != null)
            {
                Object.Destroy(instance.GameObject);
            }
        }
    }
}
