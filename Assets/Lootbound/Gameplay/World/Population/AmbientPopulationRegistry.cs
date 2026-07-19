using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Population
{
    /// <summary>State of one planned ambient presence.</summary>
    public enum AmbientPlanState
    {
        /// <summary>Intention exists; not currently materialized.</summary>
        Pending,

        /// <summary>At least one member is currently alive in the scene.</summary>
        Alive,

        /// <summary>All members were killed - never recreated this session (V1).</summary>
        Defeated,

        /// <summary>Every candidate failed structurally for this generation.</summary>
        StructurallyDead
    }

    /// <summary>
    /// The local memory of the world: which cells were planned, what lives
    /// where, what was defeated, and what a despawned creature must remember
    /// (identity + health). Pure C# - no Unity lookups, no singleton; owned
    /// by the controller and fully testable with fake instances.
    /// </summary>
    public sealed class AmbientPopulationRegistry
    {
        public sealed class PlanRecord
        {
            public AmbientPopulationPlan Plan;
            public AmbientPlanState State = AmbientPlanState.Pending;
            public readonly List<string> DeadMemberIds = new List<string>();
        }

        private sealed class CellRecord
        {
            public readonly List<PlanRecord> Plans = new List<PlanRecord>();
        }

        private readonly Dictionary<Vector2Int, CellRecord> cells = new Dictionary<Vector2Int, CellRecord>();
        private readonly Dictionary<string, IAmbientInstance> aliveByMemberId = new Dictionary<string, IAmbientInstance>();
        private readonly Dictionary<Vector2Int, List<IAmbientInstance>> aliveByCell = new Dictionary<Vector2Int, List<IAmbientInstance>>();
        private readonly Dictionary<string, int> aliveByDefinition = new Dictionary<string, int>();
        private readonly Dictionary<string, AmbientInstanceSnapshot> snapshots = new Dictionary<string, AmbientInstanceSnapshot>();

        public int TotalAlive => aliveByMemberId.Count;
        public int PlannedCellCount => cells.Count;

        // Session statistics (debug)
        public int TotalSpawned { get; private set; }
        public int TotalDespawned { get; private set; }
        public int TotalDeaths { get; private set; }

        #region Cells and plans

        public bool IsCellPlanned(Vector2Int cell) => cells.ContainsKey(cell);

        /// <summary>
        /// Store the deterministic plans of a freshly planned cell. Planning
        /// an already planned cell is a no-op (never a re-roll).
        /// </summary>
        public void StoreCellPlans(Vector2Int cell, IReadOnlyList<AmbientPopulationPlan> plans)
        {
            if (cells.ContainsKey(cell))
            {
                return;
            }

            var record = new CellRecord();
            if (plans != null)
            {
                foreach (var plan in plans)
                {
                    record.Plans.Add(new PlanRecord { Plan = plan });
                }
            }
            cells[cell] = record;
        }

        public IReadOnlyList<PlanRecord> GetCellPlans(Vector2Int cell)
        {
            return cells.TryGetValue(cell, out var record) ? record.Plans : null;
        }

        public void MarkPlanStructurallyDead(PlanRecord record)
        {
            if (record.State == AmbientPlanState.Pending)
            {
                record.State = AmbientPlanState.StructurallyDead;
            }
        }

        #endregion

        #region Alive instances

        public int AliveCount(string populationId)
        {
            return aliveByDefinition.TryGetValue(populationId, out int count) ? count : 0;
        }

        public int AliveInCell(Vector2Int cell)
        {
            return aliveByCell.TryGetValue(cell, out var list) ? list.Count : 0;
        }

        public bool IsMemberAlive(string memberId) => aliveByMemberId.ContainsKey(memberId);

        public void RegisterSpawned(PlanRecord record, IAmbientInstance instance)
        {
            if (aliveByMemberId.ContainsKey(instance.MemberId))
            {
                return; // duplicate guard
            }

            aliveByMemberId[instance.MemberId] = instance;

            if (!aliveByCell.TryGetValue(instance.Cell, out var list))
            {
                list = new List<IAmbientInstance>();
                aliveByCell[instance.Cell] = list;
            }
            list.Add(instance);

            aliveByDefinition.TryGetValue(instance.PopulationId, out int count);
            aliveByDefinition[instance.PopulationId] = count + 1;

            record.State = AmbientPlanState.Alive;
            TotalSpawned++;
        }

        /// <summary>
        /// Streaming despawn: the instance leaves the active world but the
        /// world remembers it (snapshot). The plan returns to Pending so the
        /// SAME presence can rematerialize at its anchor later.
        /// </summary>
        public void UnregisterDespawned(PlanRecord record, IAmbientInstance instance, AmbientInstanceSnapshot snapshot)
        {
            RemoveAlive(instance);
            snapshots[instance.MemberId] = snapshot;
            TotalDespawned++;

            if (record.State == AmbientPlanState.Alive && CountAliveMembers(record) == 0)
            {
                record.State = AmbientPlanState.Pending;
            }
        }

        /// <summary>
        /// Death: recorded for the whole session. When every member of the
        /// plan is dead, the presence is Defeated and never recreated (V1).
        /// </summary>
        public void MarkMemberDead(PlanRecord record, IAmbientInstance instance)
        {
            RemoveAlive(instance);
            snapshots.Remove(instance.MemberId);
            if (!record.DeadMemberIds.Contains(instance.MemberId))
            {
                record.DeadMemberIds.Add(instance.MemberId);
            }
            TotalDeaths++;

            if (record.DeadMemberIds.Count >= record.Plan.GroupSize)
            {
                record.State = AmbientPlanState.Defeated;
            }
            else if (CountAliveMembers(record) == 0)
            {
                record.State = AmbientPlanState.Pending;
            }
        }

        public bool IsMemberDead(PlanRecord record, string memberId) => record.DeadMemberIds.Contains(memberId);

        public bool TryGetSnapshot(string memberId, out AmbientInstanceSnapshot snapshot)
        {
            return snapshots.TryGetValue(memberId, out snapshot);
        }

        private int CountAliveMembers(PlanRecord record)
        {
            int count = 0;
            for (int i = 0; i < record.Plan.GroupSize; i++)
            {
                if (aliveByMemberId.ContainsKey(AmbientPopulationIds.MemberId(record.Plan.PlanId, i)))
                {
                    count++;
                }
            }
            return count;
        }

        private void RemoveAlive(IAmbientInstance instance)
        {
            if (!aliveByMemberId.Remove(instance.MemberId))
            {
                return;
            }

            if (aliveByCell.TryGetValue(instance.Cell, out var list))
            {
                list.Remove(instance);
                if (list.Count == 0) aliveByCell.Remove(instance.Cell);
            }

            if (aliveByDefinition.TryGetValue(instance.PopulationId, out int count))
            {
                if (count <= 1) aliveByDefinition.Remove(instance.PopulationId);
                else aliveByDefinition[instance.PopulationId] = count - 1;
            }
        }

        #endregion

        #region Queries

        /// <summary>
        /// Squared distance to the nearest living ambient instance within the
        /// cells around a position (never a scene-wide search).
        /// </summary>
        public bool IsAnyAliveWithin(Vector3 position, float radius, Vector3 discCenter, float cellSize)
        {
            float radiusSqr = radius * radius;
            Vector2Int center = AmbientPopulationCells.WorldToCell(position, discCenter, cellSize);
            int cellRange = Mathf.CeilToInt(radius / cellSize);

            for (int x = center.x - cellRange; x <= center.x + cellRange; x++)
            {
                for (int y = center.y - cellRange; y <= center.y + cellRange; y++)
                {
                    if (!aliveByCell.TryGetValue(new Vector2Int(x, y), out var list)) continue;
                    foreach (var instance in list)
                    {
                        if ((instance.Position - position).sqrMagnitude < radiusSqr)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>All living instances (debug/despawn iteration; copy-free).</summary>
        public Dictionary<string, IAmbientInstance>.ValueCollection AliveInstances => aliveByMemberId.Values;

        #endregion

        /// <summary>
        /// Full purge: a regeneration invalidates every cell, plan, instance
        /// record and snapshot of the previous world.
        /// </summary>
        public void Clear()
        {
            cells.Clear();
            aliveByMemberId.Clear();
            aliveByCell.Clear();
            aliveByDefinition.Clear();
            snapshots.Clear();
            TotalSpawned = 0;
            TotalDespawned = 0;
            TotalDeaths = 0;
        }
    }
}
