using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Presentation.Wildlife;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Pure tests for the bird flock planning: determinism, defensive
    /// handling of empty/invalid libraries, bounded values, trajectory
    /// coherence, shape-preserving translation and NaN-free directions.
    /// </summary>
    public class BirdWildlifeTests
    {
        private const float EPSILON = 0.0001f;
        private static readonly Vector3 Anchor = new Vector3(120f, 18f, 84f);
        private const float MinimumHeight = 20f;

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

        private GameObject CreatePrefab(string name)
        {
            var go = new GameObject(name);
            createdObjects.Add(go);
            return go;
        }

        private BirdVisualVariant CreateVariant(GameObject prefab, float weight)
        {
            var variant = new BirdVisualVariant();
            SetField(variant, "prefab", prefab);
            SetField(variant, "weight", weight);
            return variant;
        }

        private BirdVisualLibrary CreateLibrary(params BirdVisualVariant[] variants)
        {
            var library = ScriptableObject.CreateInstance<BirdVisualLibrary>();
            createdObjects.Add(library);
            SetField(library, "variants", variants);
            return library;
        }

        private BirdVisualLibrary CreateValidLibrary()
        {
            return CreateLibrary(CreateVariant(CreatePrefab("bird_a"), 1f));
        }

        #region Determinism

        [Test]
        public void SameSeed_ProducesStrictlyIdenticalPlan()
        {
            var library = CreateValidLibrary();

            Assert.IsTrue(BirdFlockPlanner.TryCreatePlan(4242, library, Anchor, MinimumHeight, false, out var a));
            Assert.IsTrue(BirdFlockPlanner.TryCreatePlan(4242, library, Anchor, MinimumHeight, false, out var b));

            Assert.AreEqual(a.VariantIndex, b.VariantIndex);
            Assert.AreEqual(a.BirdCount, b.BirdCount);
            Assert.AreEqual(a.Duration, b.Duration);
            Assert.AreEqual(a.Trajectory.Type, b.Trajectory.Type);
            Assert.AreEqual(a.Trajectory.Start, b.Trajectory.Start);
            Assert.AreEqual(a.Trajectory.Control, b.Trajectory.Control);
            Assert.AreEqual(a.Trajectory.End, b.Trajectory.End);

            for (int i = 0; i < a.BirdCount; i++)
            {
                Assert.AreEqual(a.Specs[i].FormationOffset, b.Specs[i].FormationOffset);
                Assert.AreEqual(a.Specs[i].SpeedFactor, b.Specs[i].SpeedFactor);
                Assert.AreEqual(a.Specs[i].StartDelay, b.Specs[i].StartDelay);
                Assert.AreEqual(a.Specs[i].BobPhase, b.Specs[i].BobPhase);
                Assert.AreEqual(a.Specs[i].Scale, b.Specs[i].Scale);
                Assert.AreEqual(a.Specs[i].FlapSpeed, b.Specs[i].FlapSpeed);
            }
        }

        [Test]
        public void DifferentSeeds_CanProduceDifferentPlans()
        {
            var library = CreateValidLibrary();
            var signatures = new HashSet<string>();

            for (int seed = 0; seed < 20; seed++)
            {
                Assert.IsTrue(BirdFlockPlanner.TryCreatePlan(seed, library, Anchor, MinimumHeight, false, out var plan));
                signatures.Add($"{plan.BirdCount}|{plan.Trajectory.Type}|{plan.Trajectory.Start}");
            }

            Assert.Greater(signatures.Count, 1, "20 seeds must not all collapse to one plan");
        }

        [Test]
        public void Planner_IsIndependentOfAnyCamera()
        {
            var library = CreateValidLibrary();
            var cameraGo = new GameObject("Test_Camera", typeof(Camera));
            createdObjects.Add(cameraGo);

            Assert.IsTrue(BirdFlockPlanner.TryCreatePlan(77, library, Anchor, MinimumHeight, false, out var before));
            cameraGo.transform.position = new Vector3(500f, 90f, -200f);
            Assert.IsTrue(BirdFlockPlanner.TryCreatePlan(77, library, Anchor, MinimumHeight, false, out var after));

            Assert.AreEqual(before.Trajectory.Start, after.Trajectory.Start);
            Assert.AreEqual(before.BirdCount, after.BirdCount);
        }

        #endregion

        #region Defensive library handling

        [Test]
        public void EmptyOrNullLibrary_WithoutFallback_ProducesNoPlan()
        {
            Assert.IsFalse(BirdFlockPlanner.TryCreatePlan(1, null, Anchor, MinimumHeight, false, out _));

            var empty = CreateLibrary();
            Assert.IsFalse(BirdFlockPlanner.TryCreatePlan(1, empty, Anchor, MinimumHeight, false, out _));
        }

        [Test]
        public void EmptyLibrary_WithFallback_ProducesFallbackPlan()
        {
            Assert.IsTrue(BirdFlockPlanner.TryCreatePlan(1, null, Anchor, MinimumHeight, true, out var plan));
            Assert.AreEqual(-1, plan.VariantIndex, "fallback plans carry the -1 sentinel");
            Assert.GreaterOrEqual(plan.BirdCount, 2);
            Assert.LessOrEqual(plan.BirdCount, 6);
        }

        [Test]
        public void NullPrefabEntries_AreIgnored()
        {
            var library = CreateLibrary(
                CreateVariant(null, 100f),
                CreateVariant(CreatePrefab("valid"), 1f));

            for (int seed = 0; seed < 30; seed++)
            {
                Assert.IsTrue(BirdFlockPlanner.TryCreatePlan(seed, library, Anchor, MinimumHeight, false, out var plan));
                Assert.AreEqual(1, plan.VariantIndex, "the null-prefab entry must never win, whatever its weight");
            }
        }

        [Test]
        public void InvalidWeights_AreHandledDefensively()
        {
            var library = CreateLibrary(
                CreateVariant(CreatePrefab("zero"), 0f),
                CreateVariant(CreatePrefab("nan"), float.NaN));

            Assert.IsFalse(BirdFlockPlanner.TryCreatePlan(3, library, Anchor, MinimumHeight, false, out _),
                "no valid weight means no plan without fallback");
            Assert.IsTrue(BirdFlockPlanner.TryCreatePlan(3, library, Anchor, MinimumHeight, true, out var plan));
            Assert.AreEqual(-1, plan.VariantIndex);
        }

        [Test]
        public void GroupSize_IsClamped()
        {
            var library = CreateValidLibrary();
            SetField(library, "groupSizeRange", new Vector2Int(-5, 50));

            for (int seed = 0; seed < 20; seed++)
            {
                BirdFlockPlanner.TryCreatePlan(seed, library, Anchor, MinimumHeight, false, out var plan);
                Assert.GreaterOrEqual(plan.BirdCount, 1);
                Assert.LessOrEqual(plan.BirdCount, 12, "group size is defensively clamped");
            }
        }

        #endregion

        #region Bounded values

        [Test]
        public void DurationSpeedsAndDelays_AreBounded()
        {
            var library = CreateValidLibrary();
            SetField(library, "flightDurationRange", new Vector2(-10f, 9999f));

            for (int seed = 0; seed < 20; seed++)
            {
                BirdFlockPlanner.TryCreatePlan(seed, library, Anchor, MinimumHeight, false, out var plan);
                Assert.GreaterOrEqual(plan.Duration, 2f);
                Assert.LessOrEqual(plan.Duration, 60f);
                Assert.GreaterOrEqual(plan.TotalDuration, plan.Duration);

                foreach (var spec in plan.Specs)
                {
                    Assert.GreaterOrEqual(spec.SpeedFactor, 0.5f);
                    Assert.LessOrEqual(spec.SpeedFactor, 1.5f);
                    Assert.GreaterOrEqual(spec.StartDelay, 0f);
                    Assert.LessOrEqual(spec.StartDelay, 2f);
                    Assert.Greater(spec.Scale, 0f);
                    Assert.Greater(spec.FlapSpeed, 0f);
                }
            }
        }

        [Test]
        public void FormationOffsets_AreNotUniform()
        {
            var library = CreateValidLibrary();
            SetField(library, "groupSizeRange", new Vector2Int(6, 6));

            BirdFlockPlanner.TryCreatePlan(9, library, Anchor, MinimumHeight, false, out var plan);

            var distinct = new HashSet<float>();
            foreach (var spec in plan.Specs)
            {
                distinct.Add(Mathf.Round(spec.FormationOffset.magnitude * 100f));
            }

            Assert.Greater(distinct.Count, 1, "a loose formation never has identical offsets (no perfect V)");
        }

        #endregion

        #region Trajectories

        [Test]
        public void Trajectories_AreCoherentAboveMinimumHeight()
        {
            var library = CreateValidLibrary();

            for (int seed = 0; seed < 40; seed++)
            {
                BirdFlockPlanner.TryCreatePlan(seed, library, Anchor, MinimumHeight, false, out var plan);
                var trajectory = plan.Trajectory;

                Assert.Greater((trajectory.End - trajectory.Start).magnitude, 1f, "start and end must differ");
                Assert.GreaterOrEqual(trajectory.Start.y, MinimumHeight - EPSILON);
                Assert.GreaterOrEqual(trajectory.Control.y, MinimumHeight - EPSILON);
                Assert.GreaterOrEqual(trajectory.End.y, MinimumHeight - EPSILON);
            }
        }

        [Test]
        public void Evaluate_EndpointsMatchStartAndEnd()
        {
            var trajectory = new BirdTrajectory(BirdTrajectoryType.Crossing,
                new Vector3(0f, 10f, 0f), new Vector3(5f, 14f, 5f), new Vector3(10f, 11f, 0f));

            Assert.AreEqual(trajectory.Start, trajectory.Evaluate(0f));
            Assert.AreEqual(trajectory.End, trajectory.Evaluate(1f));
        }

        [Test]
        public void Translated_PreservesShape()
        {
            var trajectory = new BirdTrajectory(BirdTrajectoryType.Circling,
                new Vector3(2f, 8f, 3f), new Vector3(9f, 15f, -4f), new Vector3(20f, 9f, 6f));
            var offset = new Vector3(-12f, 3f, 40f);
            var translated = trajectory.Translated(offset);

            foreach (float t in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                Vector3 expected = trajectory.Evaluate(t) + offset;
                Vector3 actual = translated.Evaluate(t);
                Assert.Less((expected - actual).magnitude, EPSILON, $"shape broken at t={t}");

                Assert.Less((trajectory.EvaluateDirection(t) - translated.EvaluateDirection(t)).magnitude, EPSILON,
                    "translation must not change the flight direction");
            }
        }

        [Test]
        public void EvaluateDirection_NeverNaNOrZero()
        {
            var normal = new BirdTrajectory(BirdTrajectoryType.Rising,
                new Vector3(0f, 5f, 0f), new Vector3(4f, 12f, 4f), new Vector3(12f, 20f, 8f));
            var degenerate = new BirdTrajectory(BirdTrajectoryType.Crossing,
                Vector3.one, Vector3.one, Vector3.one);

            foreach (float t in new[] { 0f, 0.5f, 1f })
            {
                foreach (var trajectory in new[] { normal, degenerate })
                {
                    Vector3 direction = trajectory.EvaluateDirection(t);
                    Assert.IsFalse(float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsNaN(direction.z));
                    Assert.Greater(direction.magnitude, 0.9f, "direction stays normalized and valid");
                }
            }
        }

        #endregion
    }
}
