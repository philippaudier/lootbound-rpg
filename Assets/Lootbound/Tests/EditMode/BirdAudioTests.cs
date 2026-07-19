using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Presentation.Audio;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Pure tests for the bird audio presentation: null-safe clip selection,
    /// deterministic picks, and defensively bounded library values.
    /// </summary>
    public class BirdAudioTests
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

        private AudioClip CreateClip(string name)
        {
            var clip = AudioClip.Create(name, 441, 1, 44100, false);
            createdObjects.Add(clip);
            return clip;
        }

        private BirdAudioLibrary CreateLibrary()
        {
            var library = ScriptableObject.CreateInstance<BirdAudioLibrary>();
            createdObjects.Add(library);
            return library;
        }

        #region Clip selection

        [Test]
        public void TryPickClip_EmptyOrNullArray_ReturnsFalseWithoutException()
        {
            var rng = new System.Random(1);
            Assert.IsFalse(BirdAudioSelection.TryPickClip(null, rng, out _));
            Assert.IsFalse(BirdAudioSelection.TryPickClip(new AudioClip[0], rng, out _));
            Assert.IsFalse(BirdAudioSelection.HasValidClip(null));
            Assert.IsFalse(BirdAudioSelection.HasValidClip(new AudioClip[0]));
        }

        [Test]
        public void TryPickClip_AllNullEntries_ReturnsFalse()
        {
            var rng = new System.Random(1);
            var clips = new AudioClip[] { null, null, null };
            Assert.IsFalse(BirdAudioSelection.TryPickClip(clips, rng, out AudioClip clip));
            Assert.IsNull(clip);
            Assert.IsFalse(BirdAudioSelection.HasValidClip(clips));
        }

        [Test]
        public void TryPickClip_MixedNullAndValid_AlwaysPicksAValidClip()
        {
            var a = CreateClip("a");
            var b = CreateClip("b");
            var clips = new AudioClip[] { null, a, null, b, null };
            var rng = new System.Random(7);

            var picked = new HashSet<AudioClip>();
            for (int i = 0; i < 200; i++)
            {
                Assert.IsTrue(BirdAudioSelection.TryPickClip(clips, rng, out AudioClip clip));
                Assert.IsNotNull(clip, "a null entry must never be selected");
                picked.Add(clip);
            }

            CollectionAssert.AreEquivalent(new[] { a, b }, picked,
                "every valid clip must be reachable, and only valid clips");
        }

        [Test]
        public void TryPickClip_IsDeterministicForAGivenSeed()
        {
            var clips = new AudioClip[] { CreateClip("a"), CreateClip("b"), CreateClip("c") };

            var firstRun = new List<AudioClip>();
            var secondRun = new List<AudioClip>();
            foreach (var run in new[] { firstRun, secondRun })
            {
                var rng = new System.Random(4242);
                for (int i = 0; i < 30; i++)
                {
                    BirdAudioSelection.TryPickClip(clips, rng, out AudioClip clip);
                    run.Add(clip);
                }
            }

            CollectionAssert.AreEqual(firstRun, secondRun);
        }

        [Test]
        public void PickInRange_StaysWithinBoundsEvenWhenUnordered()
        {
            var rng = new System.Random(9);
            for (int i = 0; i < 200; i++)
            {
                float pitch = BirdAudioSelection.PickInRange(new Vector2(0.95f, 1.05f), rng);
                Assert.GreaterOrEqual(pitch, 0.95f - EPSILON);
                Assert.LessOrEqual(pitch, 1.05f + EPSILON);

                float reversed = BirdAudioSelection.PickInRange(new Vector2(1f, 0.9f), rng);
                Assert.GreaterOrEqual(reversed, 0.9f - EPSILON);
                Assert.LessOrEqual(reversed, 1f + EPSILON);
            }
        }

        #endregion

        #region Library sanitization

        [Test]
        public void Library_Defaults_MatchAuthoredSpecification()
        {
            var library = CreateLibrary();
            Assert.AreEqual(0.95f, library.PitchRange.x, EPSILON);
            Assert.AreEqual(1.05f, library.PitchRange.y, EPSILON);
            Assert.AreEqual(0.90f, library.VolumeRange.x, EPSILON);
            Assert.AreEqual(1.00f, library.VolumeRange.y, EPSILON);
            Assert.AreEqual(8f, library.MinDistance, EPSILON);
            Assert.AreEqual(45f, library.MaxDistance, EPSILON);
            Assert.AreEqual(AudioRolloffMode.Logarithmic, library.Rolloff);
            Assert.AreEqual(1f, library.SpatialBlend, EPSILON);
            Assert.AreEqual(128, library.Priority);
        }

        [Test]
        public void Library_InvalidValues_AreDefensivelyBounded()
        {
            var library = CreateLibrary();
            SetField(library, "pitchRange", new Vector2(-2f, 0f));
            SetField(library, "volumeRange", new Vector2(3f, -1f));
            SetField(library, "minDistance", 50f);
            SetField(library, "maxDistance", 10f);
            SetField(library, "spatialBlend", 4f);
            SetField(library, "priority", 999);

            Assert.Greater(library.PitchRange.x, 0f, "pitch must stay strictly positive");
            Assert.GreaterOrEqual(library.PitchRange.y, library.PitchRange.x);
            Assert.GreaterOrEqual(library.VolumeRange.x, 0f);
            Assert.LessOrEqual(library.VolumeRange.y, 1f);
            Assert.LessOrEqual(library.VolumeRange.x, library.VolumeRange.y);
            Assert.LessOrEqual(library.MinDistance, library.MaxDistance, "ranges are reordered");
            Assert.Greater(library.MinDistance, 0f);
            Assert.AreEqual(1f, library.SpatialBlend, EPSILON, "spatial blend clamped to 0..1");
            Assert.AreEqual(256, library.Priority, "priority clamped to 0..256");
        }

        #endregion
    }
}
