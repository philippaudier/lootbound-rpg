using UnityEngine;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.World.Progression;

namespace Lootbound.Gameplay.World.Ambience
{
    /// <summary>
    /// Reads the world's ambience intent at the player position and drives
    /// the rendering through an applier, with framerate-independent temporal
    /// smoothing. Pure presentation: it never touches gameplay, spawning,
    /// difficulty or perception, and it never recomputes depth or rings -
    /// only WorldProgression.GetContext is consulted.
    ///
    /// Regeneration needs no special handling: the progression is read live
    /// from the published generation context each evaluation, and the
    /// smoothing carries the visuals continuously to the new world's values.
    /// </summary>
    public sealed class WorldAmbienceController : MonoBehaviour
    {
        private const string LogCategory = "WorldAmbience";

        [Header("Sources")]
        [SerializeField] private ProceduralTerrainGenerator terrainGenerator;
        [SerializeField] private Transform player;

        [Header("Rendering")]
        [SerializeField]
        [Tooltip("Concrete rendering integration (e.g. PBSkyWorldAmbienceApplier)")]
        private WorldAmbienceApplierBase applier;

        [SerializeField] private WorldAmbienceConfig config;

        private WorldProgression progressionOverride;
        private WorldAmbienceBaseline baseline;
        private bool baselineCaptured;
        private bool unusableLogged;
        private float nextEvaluationAt;

        private WorldAmbienceState currentState;
        private WorldAmbienceState targetState;
        private WorldRingContext lastContext;

        /// <summary>
        /// Debug-only: force the evaluated depth (0..1) instead of the player
        /// position. Simulates the visual context only - WorldProgression and
        /// gameplay are never altered.
        /// </summary>
        public float? PreviewDepthOverride { get; set; }

        #region Debug API (F7)

        public bool IsReady => baselineCaptured && ActiveProgression != null;
        public WorldAmbienceBaseline Baseline => baseline;
        public WorldAmbienceState CurrentState => currentState;
        public WorldAmbienceState TargetState => targetState;
        public WorldRingContext LastContext => lastContext;
        public string ApplierStatus => applier != null ? applier.StatusDescription : "no applier assigned";

        public float? ActualDepth01
        {
            get
            {
                var progression = ActiveProgression;
                if (progression == null || player == null) return null;
                return progression.GetContext(player.position).Depth01;
            }
        }

        #endregion

        private WorldProgression ActiveProgression =>
            progressionOverride ?? terrainGenerator?.Context?.LayoutContext?.Progression;

        /// <summary>Inject a progression source directly (targeted tests).</summary>
        public void SetProgressionSource(WorldProgression progression)
        {
            progressionOverride = progression;
        }

        private void OnDisable()
        {
            if (baselineCaptured && applier != null)
            {
                applier.Restore();
            }

            baselineCaptured = false;
            unusableLogged = false;
        }

        private void Update()
        {
            if (applier == null || config == null)
            {
                if (!unusableLogged)
                {
                    LootboundLog.Warning(LogCategory,
                        "WorldAmbienceController is inactive (missing applier or config) - visuals stay untouched");
                    unusableLogged = true;
                }
                return;
            }

            if (!baselineCaptured)
            {
                if (!applier.TryCaptureBaseline(out baseline))
                {
                    if (!unusableLogged)
                    {
                        LootboundLog.Warning(LogCategory, "Baseline capture failed - ambience stays inactive");
                        unusableLogged = true;
                    }
                    return;
                }

                baselineCaptured = true;
                currentState = WorldAmbienceState.AtBaseline(baseline);
                targetState = currentState;
            }

            float now = Time.time;
            if (now >= nextEvaluationAt)
            {
                nextEvaluationAt = now + config.EvaluationInterval;
                EvaluateTarget();
            }

            float factor = WorldAmbienceSmoothing.Factor(config.TransitionSpeed, Time.deltaTime);
            currentState = WorldAmbienceState.Interpolate(currentState, targetState, factor);
            applier.Apply(currentState);
        }

        private void EvaluateTarget()
        {
            var progression = ActiveProgression;
            if (progression == null)
            {
                // No published world yet: idle at the exact baseline.
                targetState = WorldAmbienceState.AtBaseline(baseline);
                return;
            }

            if (PreviewDepthOverride.HasValue)
            {
                float previewDepth = Mathf.Clamp01(PreviewDepthOverride.Value);
                lastContext = progression.GetContextFromDistance(previewDepth * progression.WorldDiscRadius);
            }
            else if (player != null)
            {
                lastContext = progression.GetContext(player.position);
            }
            else
            {
                targetState = WorldAmbienceState.AtBaseline(baseline);
                return;
            }

            targetState = WorldAmbienceEvaluator.Evaluate(lastContext, config, baseline);
        }
    }
}
