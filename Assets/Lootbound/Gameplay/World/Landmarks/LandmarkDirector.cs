using System;
using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// The observable runtime service for the world's landmarks - the place
    /// future systems anchor to (wildlife, merchants, encounters, lore,
    /// campfires...). It does not compute landmarks: those are the shared
    /// generation result attached to the layout. The director simply
    /// republishes them as a live, queryable registry with register/release
    /// events, the same way AmbientEventDirector exposes ambient events.
    ///
    /// It references no prefab, no Renderer, no presentation type: presenters
    /// observe it, never the reverse.
    /// </summary>
    public sealed class LandmarkDirector : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField]
        [Tooltip("Generator whose OnGenerationComplete publishes the validated layout with its landmarks")]
        private ProceduralTerrainGenerator terrainGenerator;

        private readonly List<LandmarkIdentity> active = new List<LandmarkIdentity>();
        private ProceduralTerrainGenerator boundGenerator;

        /// <summary>The landmarks currently present in the world.</summary>
        public IReadOnlyList<LandmarkIdentity> ActiveLandmarks => active;

        /// <summary>Count of active landmarks (tests and inspection).</summary>
        public int ActiveLandmarkCount => active.Count;

        /// <summary>A landmark now exists in the world.</summary>
        public event Action<LandmarkIdentity> OnLandmarkRegistered;

        /// <summary>A landmark is gone (regeneration or teardown).</summary>
        public event Action<LandmarkIdentity> OnLandmarkReleased;

        private void OnEnable()
        {
            BindGenerator(terrainGenerator);

            // Catch up if generation already happened before this component
            // subscribed (its landmarks are already on the layout).
            if (boundGenerator != null && boundGenerator.IsGenerated && active.Count == 0)
            {
                Publish(boundGenerator.Context);
            }
        }

        private void OnDisable()
        {
            UnbindGenerator();
            ReleaseAll();
        }

        /// <summary>Rebind to a generator at runtime (releases everything first).</summary>
        public void SetGenerator(ProceduralTerrainGenerator generator)
        {
            UnbindGenerator();
            ReleaseAll();

            terrainGenerator = generator;
            if (isActiveAndEnabled)
            {
                BindGenerator(generator);
                if (generator != null && generator.IsGenerated)
                {
                    Publish(generator.Context);
                }
            }
        }

        private void BindGenerator(ProceduralTerrainGenerator generator)
        {
            if (generator == null || boundGenerator == generator)
            {
                return;
            }

            boundGenerator = generator;
            boundGenerator.OnGenerationComplete += Publish;
        }

        private void UnbindGenerator()
        {
            if (boundGenerator == null)
            {
                return;
            }

            boundGenerator.OnGenerationComplete -= Publish;
            boundGenerator = null;
        }

        private void Publish(TerrainGenerationContext context)
        {
            // Regeneration: release the previous set, then republish the new
            // one. Landmarks were computed once and attached to the layout
            // (before this event), so the set is complete and order-stable.
            ReleaseAll();

            var layout = context != null ? context.LayoutContext : null;
            if (layout == null)
            {
                return;
            }

            foreach (var landmark in layout.Landmarks)
            {
                active.Add(landmark);
                OnLandmarkRegistered?.Invoke(landmark);
            }
        }

        private void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var landmark = active[i];
                active.RemoveAt(i);
                OnLandmarkReleased?.Invoke(landmark);
            }
        }

        #region Queries (for future anchoring systems)

        /// <summary>Nearest landmark to a world position, or null when there are none.</summary>
        public LandmarkIdentity GetNearest(Vector3 position)
        {
            LandmarkIdentity nearest = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < active.Count; i++)
            {
                float sq = (active[i].Position - position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    nearest = active[i];
                }
            }

            return nearest;
        }

        /// <summary>All landmarks in a given ring.</summary>
        public IEnumerable<LandmarkIdentity> GetByRing(WorldRing ring)
        {
            foreach (var landmark in active)
            {
                if (landmark.Ring == ring)
                {
                    yield return landmark;
                }
            }
        }

        #endregion
    }
}
