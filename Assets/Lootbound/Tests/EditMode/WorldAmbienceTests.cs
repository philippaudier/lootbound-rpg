using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World.Ambience;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Progression;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Pure tests for the ambience translation layer: evaluator determinism,
    /// exact baseline preservation at Depth 0, documented extremes at full
    /// intent, NaN guards, framerate-independent smoothing, interpolation
    /// bounds, and baseline value guards.
    /// </summary>
    public class WorldAmbienceTests
    {
        private const float EPSILON = 0.0001f;

        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in createdObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            createdObjects.Clear();
        }

        private WorldAmbienceConfig CreateDefaultConfig()
        {
            var config = ScriptableObject.CreateInstance<WorldAmbienceConfig>();
            createdObjects.Add(config);
            return config;
        }

        private static WorldRingContext MakeContext(
            float depth, float fog, float attenuation, float saturation, float temperature)
        {
            return new WorldRingContext(
                WorldRing.Nearlands, depth * 512f, depth, depth <= 1f,
                depth, 0, fog, attenuation, saturation, temperature);
        }

        private static readonly WorldAmbienceBaseline SceneBaseline =
            new WorldAmbienceBaseline(400f, new Color(0.9f, 0.92f, 1f), 5000f);

        #region Evaluator

        [Test]
        public void Evaluate_SameInputs_IsDeterministic()
        {
            var config = CreateDefaultConfig();
            var context = MakeContext(0.6f, 0.55f, 0.3f, 0.75f, 0.4f);

            var a = WorldAmbienceEvaluator.Evaluate(context, config, SceneBaseline);
            var b = WorldAmbienceEvaluator.Evaluate(context, config, SceneBaseline);

            Assert.AreEqual(a.MeanFreePath, b.MeanFreePath);
            Assert.AreEqual(a.FogTint, b.FogTint);
            Assert.AreEqual(a.DirectionalMultiplier, b.DirectionalMultiplier);
            Assert.AreEqual(a.AmbientMultiplier, b.AmbientMultiplier);
            Assert.AreEqual(a.SaturationOffset, b.SaturationOffset);
            Assert.AreEqual(a.TemperatureOffset, b.TemperatureOffset);
            Assert.AreEqual(a.ContrastOffset, b.ContrastOffset);
        }

        [Test]
        public void Evaluate_DepthZero_NeutralIntents_ReproducesBaselineExactly()
        {
            var config = CreateDefaultConfig();
            // Neutral intents: no fog, no attenuation, fully natural color.
            var context = MakeContext(0f, 0f, 0f, 1f, 0.5f);

            var state = WorldAmbienceEvaluator.Evaluate(context, config, SceneBaseline);

            Assert.AreEqual(SceneBaseline.MeanFreePath, state.MeanFreePath, EPSILON);
            Assert.AreEqual(SceneBaseline.FogTint, state.FogTint);
            Assert.AreEqual(SceneBaseline.MaxFogDistance, state.MaxFogDistance, EPSILON);
            Assert.AreEqual(1f, state.DirectionalMultiplier, EPSILON);
            Assert.AreEqual(1f, state.AmbientMultiplier, EPSILON);
            Assert.AreEqual(0f, state.SaturationOffset, EPSILON);
            Assert.AreEqual(0f, state.ContrastOffset, EPSILON);
        }

        [Test]
        public void Evaluate_RefugeTemperatureIntent_StaysImperceptible()
        {
            var config = CreateDefaultConfig();
            // The default refuge intent Temperature01 = 0.7 lands at
            // lerp(-7, +2, 0.7) = -0.7: documented as imperceptible.
            var context = MakeContext(0f, 0f, 0f, 1f, 0.7f);

            var state = WorldAmbienceEvaluator.Evaluate(context, config, SceneBaseline);

            Assert.AreEqual(-0.7f, state.TemperatureOffset, 0.01f);
        }

        [Test]
        public void Evaluate_FullIntents_ReachConfiguredExtremes()
        {
            var config = CreateDefaultConfig();
            var context = MakeContext(1f, 1f, 1f, 0f, 0f);

            var state = WorldAmbienceEvaluator.Evaluate(context, config, SceneBaseline);

            // Documented extremes of the default config asset.
            Assert.AreEqual(160f, state.MeanFreePath, EPSILON, "mean free path at full fog intent");
            Assert.AreEqual(0.70f, state.DirectionalMultiplier, EPSILON, "directional at full attenuation");
            Assert.AreEqual(0.60f, state.AmbientMultiplier, EPSILON, "ambient at full attenuation");
            Assert.AreEqual(-30f, state.SaturationOffset, EPSILON, "saturation at zero intent");
            Assert.AreEqual(-7f, state.TemperatureOffset, EPSILON, "temperature at full cold");
            Assert.AreEqual(4f, state.ContrastOffset, EPSILON, "contrast at depth 1");
        }

        [Test]
        public void Evaluate_WarmTemperatureIntent_ReachesConfiguredMaximum()
        {
            var config = CreateDefaultConfig();
            var context = MakeContext(0f, 0f, 0f, 1f, 1f);

            var state = WorldAmbienceEvaluator.Evaluate(context, config, SceneBaseline);

            Assert.AreEqual(2f, state.TemperatureOffset, EPSILON);
        }

        [Test]
        public void Evaluate_EdgelandsDefaultIntents_MatchDocumentedComposition()
        {
            var config = CreateDefaultConfig();
            // Default progression intents at the disc edge: attenuation 0.5,
            // saturation 0.6, temperature 0.25. Composition intent x translation
            // gives the documented visual endpoints.
            var context = MakeContext(1f, 1f, 0.5f, 0.6f, 0.25f);

            var state = WorldAmbienceEvaluator.Evaluate(context, config, SceneBaseline);

            Assert.AreEqual(0.85f, state.DirectionalMultiplier, EPSILON);
            Assert.AreEqual(0.80f, state.AmbientMultiplier, EPSILON);
            Assert.AreEqual(-12f, state.SaturationOffset, EPSILON);
            Assert.AreEqual(-4.75f, state.TemperatureOffset, EPSILON);
        }

        [Test]
        public void Evaluate_NullConfig_ReturnsBaselineState()
        {
            var context = MakeContext(1f, 1f, 1f, 0f, 0f);

            var state = WorldAmbienceEvaluator.Evaluate(context, null, SceneBaseline);

            Assert.AreEqual(SceneBaseline.MeanFreePath, state.MeanFreePath, EPSILON);
            Assert.AreEqual(SceneBaseline.FogTint, state.FogTint);
            Assert.AreEqual(1f, state.DirectionalMultiplier, EPSILON);
            Assert.AreEqual(1f, state.AmbientMultiplier, EPSILON);
            Assert.AreEqual(0f, state.SaturationOffset, EPSILON);
            Assert.AreEqual(0f, state.TemperatureOffset, EPSILON);
            Assert.IsFalse(state.ControlMaxFogDistance);
        }

        [Test]
        public void Evaluate_NaNIntents_FallBackWithoutProducingNaN()
        {
            var config = CreateDefaultConfig();
            var context = MakeContext(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);

            var state = WorldAmbienceEvaluator.Evaluate(context, config, SceneBaseline);

            Assert.IsFalse(float.IsNaN(state.MeanFreePath));
            Assert.IsFalse(float.IsNaN(state.DirectionalMultiplier));
            Assert.IsFalse(float.IsNaN(state.AmbientMultiplier));
            Assert.IsFalse(float.IsNaN(state.SaturationOffset));
            Assert.IsFalse(float.IsNaN(state.TemperatureOffset));
            Assert.IsFalse(float.IsNaN(state.ContrastOffset));
            // Fallbacks are the neutral intents.
            Assert.AreEqual(SceneBaseline.MeanFreePath, state.MeanFreePath, EPSILON);
            Assert.AreEqual(0f, state.SaturationOffset, EPSILON);
        }

        [Test]
        public void Evaluate_FogDistancesStayPositive()
        {
            var config = CreateDefaultConfig();
            var lowBaseline = new WorldAmbienceBaseline(25f, Color.white, 60f);

            foreach (float fog in new[] { 0f, 0.5f, 1f })
            {
                var state = WorldAmbienceEvaluator.Evaluate(
                    MakeContext(1f, fog, 0f, 1f, 0.5f), config, lowBaseline);
                Assert.GreaterOrEqual(state.MeanFreePath, 20f);
                Assert.Greater(state.MaxFogDistance, 0f);
            }
        }

        [Test]
        public void Evaluate_ControlMaxFogDistanceOff_KeepsBaselineDistance()
        {
            var config = CreateDefaultConfig();
            var context = MakeContext(1f, 1f, 1f, 0f, 0f);

            var state = WorldAmbienceEvaluator.Evaluate(context, config, SceneBaseline);

            Assert.IsFalse(state.ControlMaxFogDistance, "V1 default must not drive maxFogDistance");
            Assert.AreEqual(SceneBaseline.MaxFogDistance, state.MaxFogDistance, EPSILON);
        }

        [Test]
        public void Evaluate_TintAtDepthZero_IsExactBaselineTint()
        {
            var config = CreateDefaultConfig();
            var warmBaseline = new WorldAmbienceBaseline(400f, new Color(1f, 0.85f, 0.7f), 5000f);

            var state = WorldAmbienceEvaluator.Evaluate(
                MakeContext(0f, 0f, 0f, 1f, 0.5f), config, warmBaseline);

            Assert.AreEqual(warmBaseline.FogTint, state.FogTint);
        }

        [Test]
        public void Evaluate_TintAtFullDepth_DriftsBoundedByInfluence()
        {
            var config = CreateDefaultConfig();
            var baselineTint = SceneBaseline.FogTint;

            var state = WorldAmbienceEvaluator.Evaluate(
                MakeContext(1f, 1f, 0.5f, 0.6f, 0.25f), config, SceneBaseline);

            // Default influence 0.5: the tint moves, but stays halfway between
            // the baseline and the gradient - never a full replacement.
            Assert.AreNotEqual(baselineTint, state.FogTint);
            Color gradientEnd = config.FogTintByDepth.Evaluate(1f);
            Color expected = Color.Lerp(baselineTint, gradientEnd, 0.5f);
            Assert.AreEqual(expected.r, state.FogTint.r, 0.001f);
            Assert.AreEqual(expected.g, state.FogTint.g, 0.001f);
            Assert.AreEqual(expected.b, state.FogTint.b, 0.001f);
        }

        [Test]
        public void Evaluate_OutsideDisc_InheritsEdgeMaximum()
        {
            var config = CreateDefaultConfig();
            var progression = new WorldProgression(
                new Vector3(512f, 10f, 512f), 512f, WorldRingConfig.CreateDefault());

            var atEdge = WorldAmbienceEvaluator.Evaluate(
                progression.GetContextFromDistance(512f), config, SceneBaseline);
            var beyond = WorldAmbienceEvaluator.Evaluate(
                progression.GetContextFromDistance(512f * 1.5f), config, SceneBaseline);

            Assert.AreEqual(atEdge.MeanFreePath, beyond.MeanFreePath, EPSILON);
            Assert.AreEqual(atEdge.DirectionalMultiplier, beyond.DirectionalMultiplier, EPSILON);
            Assert.AreEqual(atEdge.SaturationOffset, beyond.SaturationOffset, EPSILON);
            Assert.AreEqual(atEdge.TemperatureOffset, beyond.TemperatureOffset, EPSILON);
            Assert.AreEqual(atEdge.ContrastOffset, beyond.ContrastOffset, EPSILON);
        }

        #endregion

        #region Baseline and state

        [Test]
        public void Baseline_GuardsInvalidValues()
        {
            var zero = new WorldAmbienceBaseline(0f, Color.white, 0f);
            Assert.AreEqual(400f, zero.MeanFreePath);
            Assert.AreEqual(5000f, zero.MaxFogDistance);

            var nan = new WorldAmbienceBaseline(float.NaN, Color.white, float.NaN);
            Assert.AreEqual(400f, nan.MeanFreePath);
            Assert.AreEqual(5000f, nan.MaxFogDistance);

            var valid = new WorldAmbienceBaseline(320f, Color.white, 4000f);
            Assert.AreEqual(320f, valid.MeanFreePath);
            Assert.AreEqual(4000f, valid.MaxFogDistance);
        }

        [Test]
        public void AtBaseline_IsNeutral()
        {
            var state = WorldAmbienceState.AtBaseline(SceneBaseline);

            Assert.AreEqual(SceneBaseline.MeanFreePath, state.MeanFreePath);
            Assert.AreEqual(SceneBaseline.FogTint, state.FogTint);
            Assert.AreEqual(1f, state.DirectionalMultiplier);
            Assert.AreEqual(1f, state.AmbientMultiplier);
            Assert.AreEqual(0f, state.SaturationOffset);
            Assert.AreEqual(0f, state.TemperatureOffset);
            Assert.AreEqual(0f, state.ContrastOffset);
            Assert.IsFalse(state.ControlMaxFogDistance);
        }

        [Test]
        public void Interpolate_FactorBounds_ReturnEndpoints()
        {
            var current = WorldAmbienceState.AtBaseline(SceneBaseline);
            var target = new WorldAmbienceState(
                160f, Color.grey, 900f, false, 0.7f, 0.6f, -30f, -7f, 4f);

            var atZero = WorldAmbienceState.Interpolate(current, target, 0f);
            Assert.AreEqual(current.MeanFreePath, atZero.MeanFreePath, EPSILON);
            Assert.AreEqual(current.SaturationOffset, atZero.SaturationOffset, EPSILON);

            var atOne = WorldAmbienceState.Interpolate(current, target, 1f);
            Assert.AreEqual(target.MeanFreePath, atOne.MeanFreePath, EPSILON);
            Assert.AreEqual(target.SaturationOffset, atOne.SaturationOffset, EPSILON);
        }

        [Test]
        public void Interpolate_ClampsFactor_NeverOvershoots()
        {
            var current = WorldAmbienceState.AtBaseline(SceneBaseline);
            var target = new WorldAmbienceState(
                160f, Color.grey, 900f, false, 0.7f, 0.6f, -30f, -7f, 4f);

            var beyond = WorldAmbienceState.Interpolate(current, target, 2.5f);
            Assert.AreEqual(target.MeanFreePath, beyond.MeanFreePath, EPSILON);
            Assert.AreEqual(target.DirectionalMultiplier, beyond.DirectionalMultiplier, EPSILON);

            var below = WorldAmbienceState.Interpolate(current, target, -1f);
            Assert.AreEqual(current.MeanFreePath, below.MeanFreePath, EPSILON);
        }

        #endregion

        #region Smoothing

        [Test]
        public void Smoothing_Factor_IsFramerateIndependent()
        {
            const float speed = 0.4f;
            const float dt = 0.2f;

            // Remaining distance after one step of dt must equal the remaining
            // distance after two steps of dt/2 (multiplicative decay).
            float oneStep = 1f - WorldAmbienceSmoothing.Factor(speed, dt);
            float halfStep = 1f - WorldAmbienceSmoothing.Factor(speed, dt * 0.5f);
            float twoSteps = halfStep * halfStep;

            Assert.AreEqual(oneStep, twoSteps, EPSILON);
        }

        [Test]
        public void Smoothing_Factor_ConvergesWithoutOvershoot()
        {
            const float speed = 0.4f;
            float value = 400f;
            const float target = 160f;

            float previousDistance = Mathf.Abs(value - target);
            for (int i = 0; i < 200; i++)
            {
                float factor = WorldAmbienceSmoothing.Factor(speed, 1f / 30f);
                value = Mathf.Lerp(value, target, factor);
                float distance = Mathf.Abs(value - target);
                Assert.LessOrEqual(distance, previousDistance + EPSILON, "must approach monotonically");
                previousDistance = distance;
            }

            // ~6.6s of simulated time at speed 0.4: most of the way there.
            Assert.Less(previousDistance, 240f * 0.15f);
        }

        [Test]
        public void Smoothing_InvalidSpeedOrDeltaTime_SnapsToTarget()
        {
            Assert.AreEqual(1f, WorldAmbienceSmoothing.Factor(0f, 0.02f));
            Assert.AreEqual(1f, WorldAmbienceSmoothing.Factor(-1f, 0.02f));
            Assert.AreEqual(1f, WorldAmbienceSmoothing.Factor(float.NaN, 0.02f));
            Assert.AreEqual(1f, WorldAmbienceSmoothing.Factor(0.4f, -0.5f));
            Assert.AreEqual(1f, WorldAmbienceSmoothing.Factor(0.4f, float.NaN));
        }

        [Test]
        public void Smoothing_Factor_StaysWithinUnitInterval()
        {
            foreach (float speed in new[] { 0.05f, 0.4f, 5f, 1000f })
            {
                foreach (float dt in new[] { 0f, 0.001f, 1f / 240f, 1f / 30f, 0.5f, 10f })
                {
                    float factor = WorldAmbienceSmoothing.Factor(speed, dt);
                    Assert.GreaterOrEqual(factor, 0f);
                    Assert.LessOrEqual(factor, 1f);
                }
            }
        }

        #endregion
    }
}
