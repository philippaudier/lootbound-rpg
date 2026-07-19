using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World.Ambience;
using Lootbound.Gameplay.World.Ambience.Events;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Progression;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Pure tests for the ambient event foundation: activity intents,
    /// single-application selection formula, deterministic weighted picks,
    /// and area-uniform ring placement.
    /// </summary>
    public class AmbientEventTests
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

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private AmbientEventProfile CreateProfile(
            string id, AmbientEventCategory category = AmbientEventCategory.Birds,
            float weight = 1f, AnimationCurve response = null,
            Vector2? distance = null, Vector2? heightOffset = null, bool avoidView = false)
        {
            var profile = ScriptableObject.CreateInstance<AmbientEventProfile>();
            createdObjects.Add(profile);
            SetField(profile, "eventId", id);
            SetField(profile, "category", category);
            SetField(profile, "weight", weight);
            SetField(profile, "activityResponse", response ?? AnimationCurve.Constant(0f, 1f, 1f));
            SetField(profile, "distanceRange", distance ?? new Vector2(8f, 30f));
            SetField(profile, "heightOffsetRange", heightOffset ?? Vector2.zero);
            SetField(profile, "avoidPlayerView", avoidView);
            return profile;
        }

        private static WorldAmbienceState MakeState(float bird, float insect, float wind, float rare)
        {
            return new WorldAmbienceState(
                400f, Color.white, 5000f, false, 1f, 1f, 0f, 0f, 0f,
                Color.white, Color.white, 1f, 0f, false,
                bird, insect, wind, rare);
        }

        private WorldAmbienceConfig CreateDefaultConfig()
        {
            var config = ScriptableObject.CreateInstance<WorldAmbienceConfig>();
            createdObjects.Add(config);
            return config;
        }

        private static WorldRingContext ContextAtDepth(float depth)
        {
            return new WorldRingContext(
                WorldRing.Nearlands, depth * 512f, depth, depth <= 1f,
                depth, 0, depth, depth * 0.5f, 1f - depth * 0.4f, 0.7f - depth * 0.45f);
        }

        #region Activities

        [Test]
        public void Activities_AtRefugeDepth_MatchAuthoredRefugeValues()
        {
            var config = CreateDefaultConfig();
            var state = WorldAmbienceEvaluator.Evaluate(
                ContextAtDepth(0f), config, WorldAmbienceBaseline.Default);

            Assert.AreEqual(1f, state.BirdActivity, EPSILON);
            Assert.AreEqual(1f, state.InsectActivity, EPSILON);
            Assert.AreEqual(0.20f, state.WindActivity, EPSILON);
            Assert.AreEqual(0.02f, state.RareEventActivity, EPSILON);
        }

        [Test]
        public void Activities_AtFullDepth_MatchAuthoredDeepValues()
        {
            var config = CreateDefaultConfig();
            var state = WorldAmbienceEvaluator.Evaluate(
                ContextAtDepth(1f), config, WorldAmbienceBaseline.Default);

            Assert.AreEqual(0.05f, state.BirdActivity, EPSILON);
            Assert.AreEqual(0f, state.InsectActivity, EPSILON);
            Assert.AreEqual(0.85f, state.WindActivity, EPSILON);
            Assert.AreEqual(0.30f, state.RareEventActivity, EPSILON);
        }

        [Test]
        public void Activities_AlwaysStayWithinUnitInterval()
        {
            var config = CreateDefaultConfig();
            foreach (float depth in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f, float.NaN })
            {
                var state = WorldAmbienceEvaluator.Evaluate(
                    ContextAtDepth(depth), config, WorldAmbienceBaseline.Default);
                foreach (float activity in new[]
                         { state.BirdActivity, state.InsectActivity, state.WindActivity, state.RareEventActivity })
                {
                    Assert.IsFalse(float.IsNaN(activity));
                    Assert.GreaterOrEqual(activity, 0f);
                    Assert.LessOrEqual(activity, 1f);
                }
            }
        }

        [Test]
        public void Activities_Interpolate_IsStableWithoutOvershoot()
        {
            var current = MakeState(1f, 1f, 0.2f, 0.02f);
            var target = MakeState(0.05f, 0f, 0.85f, 0.3f);

            var beyond = WorldAmbienceState.Interpolate(current, target, 2f);
            Assert.AreEqual(target.BirdActivity, beyond.BirdActivity, EPSILON, "no overshoot");
            Assert.AreEqual(target.WindActivity, beyond.WindActivity, EPSILON);

            var half = WorldAmbienceState.Interpolate(current, target, 0.5f);
            Assert.AreEqual(0.525f, half.BirdActivity, EPSILON);
            Assert.AreEqual(0.525f, half.WindActivity, EPSILON);
        }

        #endregion

        #region Selection

        [Test]
        public void EffectiveWeight_AppliesActivityExactlyOnce()
        {
            // weight 2 x clamp01(curve(activity)) - and nothing else.
            var flatHalf = CreateProfile("flat", weight: 2f,
                response: AnimationCurve.Constant(0f, 1f, 0.5f));
            var state = MakeState(0.8f, 0f, 0f, 0f);
            Assert.AreEqual(1f, AmbientEventSelector.EffectiveWeight(flatHalf, state), EPSILON);

            var identity = CreateProfile("identity", weight: 2f,
                response: AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var lowActivity = MakeState(0.3f, 0f, 0f, 0f);
            Assert.AreEqual(0.6f, AmbientEventSelector.EffectiveWeight(identity, lowActivity), 0.001f);
        }

        [Test]
        public void GetActivity_MapsEachCategoryToItsIntent()
        {
            var state = MakeState(0.1f, 0.2f, 0.3f, 0.4f);
            Assert.AreEqual(0.1f, AmbientEventSelector.GetActivity(state, AmbientEventCategory.Birds), EPSILON);
            Assert.AreEqual(0.2f, AmbientEventSelector.GetActivity(state, AmbientEventCategory.Insects), EPSILON);
            Assert.AreEqual(0.3f, AmbientEventSelector.GetActivity(state, AmbientEventCategory.Wind), EPSILON);
            Assert.AreEqual(0.4f, AmbientEventSelector.GetActivity(state, AmbientEventCategory.Environmental), EPSILON);
            Assert.AreEqual(0.4f, AmbientEventSelector.GetActivity(state, AmbientEventCategory.Rare), EPSILON);
        }

        [Test]
        public void TrySelect_IsDeterministicForAGivenSeed()
        {
            var profiles = new List<AmbientEventProfile>
            {
                CreateProfile("a", weight: 1f),
                CreateProfile("b", weight: 2f),
                CreateProfile("c", weight: 0.5f)
            };
            var state = MakeState(1f, 1f, 1f, 1f);

            var firstRun = new List<string>();
            var secondRun = new List<string>();
            foreach (var run in new[] { firstRun, secondRun })
            {
                var rng = new System.Random(1234);
                for (int i = 0; i < 30; i++)
                {
                    bool selected = AmbientEventSelector.TrySelect(
                        profiles, state, null, 0.5f, rng, out var profile, out _);
                    run.Add(selected ? profile.EventId : "-");
                }
            }

            CollectionAssert.AreEqual(firstRun, secondRun, "same seed must produce the same sequence");
        }

        [Test]
        public void TrySelect_SelectsProportionallyToEffectiveWeights()
        {
            var heavy = CreateProfile("heavy", weight: 3f);
            var light = CreateProfile("light", weight: 1f);
            var profiles = new List<AmbientEventProfile> { heavy, light };
            var state = MakeState(1f, 1f, 1f, 1f);
            var rng = new System.Random(99);

            int heavyCount = 0, total = 0;
            for (int i = 0; i < 3000; i++)
            {
                if (AmbientEventSelector.TrySelect(profiles, state, null, 1f, rng, out var profile, out _))
                {
                    total++;
                    if (profile == heavy) heavyCount++;
                }
            }

            Assert.Greater(total, 2900, "chance 1 with weights >= 1 must almost always attempt");
            float ratio = (float)heavyCount / total;
            Assert.AreEqual(0.75f, ratio, 0.05f, "selection must follow effective weights");
        }

        [Test]
        public void TrySelect_IneligibleProfile_IsNeverSelected()
        {
            var blocked = CreateProfile("blocked", weight: 100f);
            var open = CreateProfile("open", weight: 0.1f);
            var profiles = new List<AmbientEventProfile> { blocked, open };
            var state = MakeState(1f, 1f, 1f, 1f);
            var rng = new System.Random(7);

            for (int i = 0; i < 200; i++)
            {
                if (AmbientEventSelector.TrySelect(
                        profiles, state, p => p != blocked, 1f, rng, out var profile, out _))
                {
                    Assert.AreSame(open, profile, "a profile filtered by eligibility (cooldown/concurrency) must never win");
                }
            }
        }

        [Test]
        public void TrySelect_ZeroActivity_YieldsNoEligibleProfile()
        {
            var profiles = new List<AmbientEventProfile>
            {
                CreateProfile("silent", response: AnimationCurve.Constant(0f, 1f, 0f))
            };
            var state = MakeState(1f, 1f, 1f, 1f);
            var rng = new System.Random(1);

            bool selected = AmbientEventSelector.TrySelect(
                profiles, state, null, 1f, rng, out _, out bool hadEligible);

            Assert.IsFalse(selected);
            Assert.IsFalse(hadEligible, "zero effective weight means nothing can spawn");
        }

        [Test]
        public void TrySelect_NoProfiles_ReturnsCleanly()
        {
            var state = MakeState(1f, 1f, 1f, 1f);
            var rng = new System.Random(1);

            Assert.IsFalse(AmbientEventSelector.TrySelect(
                new List<AmbientEventProfile>(), state, null, 1f, rng, out _, out bool hadEligible));
            Assert.IsFalse(hadEligible);
        }

        #endregion

        #region Placement

        [Test]
        public void Placement_AlwaysWithinAuthorizedRing()
        {
            var profile = CreateProfile("ring", distance: new Vector2(10f, 30f));
            var rng = new System.Random(42);
            var player = new Vector3(100f, 5f, 100f);

            for (int i = 0; i < 1000; i++)
            {
                Assert.IsTrue(AmbientEventPlacement.TryResolvePosition(
                    player, Vector3.forward, profile, rng, null, out Vector3 position));

                float horizontal = Vector2.Distance(
                    new Vector2(player.x, player.z), new Vector2(position.x, position.z));
                Assert.GreaterOrEqual(horizontal, 10f - EPSILON);
                Assert.LessOrEqual(horizontal, 30f + EPSILON);
            }
        }

        [Test]
        public void Placement_RadialDistribution_IsAreaUniform()
        {
            // Area-uniform over [10, 30]: P(r < 20) = (20^2-10^2)/(30^2-10^2) = 0.375.
            var profile = CreateProfile("area", distance: new Vector2(10f, 30f));
            var rng = new System.Random(2024);
            var player = Vector3.zero;

            int samples = 4000;
            int below20 = 0;
            for (int i = 0; i < samples; i++)
            {
                AmbientEventPlacement.TryResolvePosition(
                    player, Vector3.forward, profile, rng, null, out Vector3 position);
                if (new Vector2(position.x, position.z).magnitude < 20f) below20++;
            }

            Assert.AreEqual(0.375f, (float)below20 / samples, 0.05f,
                "radius must follow the area-uniform distribution, not a linear one (linear would give 0.5)");
        }

        [Test]
        public void Placement_AvoidPlayerView_ExcludesFrontalCone()
        {
            var profile = CreateProfile("shy", distance: new Vector2(10f, 20f), avoidView: true);
            var rng = new System.Random(5);
            var player = Vector3.zero;
            var forward = Vector3.forward;

            for (int i = 0; i < 500; i++)
            {
                AmbientEventPlacement.TryResolvePosition(
                    player, forward, profile, rng, null, out Vector3 position);

                var direction = new Vector2(position.x, position.z).normalized;
                float angle = Vector2.Angle(new Vector2(forward.x, forward.z), direction);
                Assert.GreaterOrEqual(angle, AmbientEventPlacement.ViewConeHalfAngle - 0.01f,
                    $"position at {angle:F1} degrees sits inside the excluded view cone");
            }
        }

        [Test]
        public void Placement_UsesGroundSamplerAndHeightOffset()
        {
            var profile = CreateProfile("perch", distance: new Vector2(5f, 10f),
                heightOffset: new Vector2(2f, 4f));
            var rng = new System.Random(3);
            var player = new Vector3(0f, 50f, 0f);

            bool Ground(float x, float z, out float y) { y = 7f; return true; }

            for (int i = 0; i < 100; i++)
            {
                AmbientEventPlacement.TryResolvePosition(
                    player, Vector3.forward, profile, rng, Ground, out Vector3 position);
                Assert.GreaterOrEqual(position.y, 9f - EPSILON, "ground 7 + offset min 2");
                Assert.LessOrEqual(position.y, 11f + EPSILON, "ground 7 + offset max 4");
            }

            // Without a sampler the player's height is the fallback ground.
            AmbientEventPlacement.TryResolvePosition(
                player, Vector3.forward, profile, rng, null, out Vector3 fallback);
            Assert.GreaterOrEqual(fallback.y, 52f - EPSILON);
            Assert.LessOrEqual(fallback.y, 54f + EPSILON);
        }

        [Test]
        public void Placement_InvalidInputs_FailCleanly()
        {
            var profile = CreateProfile("valid");
            Assert.IsFalse(AmbientEventPlacement.TryResolvePosition(
                Vector3.zero, Vector3.forward, null, new System.Random(1), null, out _));
            Assert.IsFalse(AmbientEventPlacement.TryResolvePosition(
                Vector3.zero, Vector3.forward, profile, null, null, out _));
        }

        #endregion
    }
}
