using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Landmarks;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Progression;
using Lootbound.Gameplay.World.Spawning;
using Lootbound.Presentation.Landmarks;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for the landmark director (observable registry) and the
    /// landmark presenter (visuals). The rig mocks a generator by injecting a
    /// context whose layout already carries hand-built landmarks - no real
    /// terrain needed - and drives the director via its enable catch-up path.
    /// </summary>
    public class LandmarkPlayModeTests
    {
        private const float DiscRadius = 512f;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            spawned.Clear();

            foreach (var asset in assets)
            {
                if (asset != null) Object.Destroy(asset);
            }
            assets.Clear();
        }

        private GameObject Track(GameObject go) { spawned.Add(go); return go; }
        private T TrackAsset<T>(T asset) where T : Object { assets.Add(asset); return asset; }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private static T GetField<T>(object obj, string fieldName) where T : class
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            return field.GetValue(obj) as T;
        }

        #region Rig

        private LandmarkIdentity MakeLandmark(string hostNode, string definitionId, Vector3 position,
            WorldRing ring = WorldRing.Wildlands)
        {
            return new LandmarkIdentity(
                $"landmark_1_{hostNode}_0", definitionId, position, ring, 0.5f, 0.5f,
                "path_1_0", hostNode, 0, 120f);
        }

        /// <summary>A generator mock: a real component with an injected, already-generated context.</summary>
        private ProceduralTerrainGenerator CreateGenerator(params LandmarkIdentity[] landmarks)
        {
            var ringConfig = WorldRingConfig.CreateDefault();
            var layout = new WorldLayoutContext(1, 0, 1, DiscRadius, Vector3.zero, ringConfig);
            layout.AttachProgression(new WorldProgression(Vector3.zero, DiscRadius, ringConfig));
            layout.AttachLandmarks(new List<LandmarkIdentity>(landmarks));

            var context = new TerrainGenerationContext(1, 33, 1024f, 150f, 1);
            context.LayoutContext = layout;

            var generatorGo = Track(new GameObject("Test_Generator"));
            generatorGo.SetActive(false);
            var generator = generatorGo.AddComponent<ProceduralTerrainGenerator>();
            SetField(generator, "context", context);
            SetField(generator, "isGenerated", true);
            generatorGo.SetActive(true);
            return generator;
        }

        private LandmarkDirector CreateDirector(ProceduralTerrainGenerator generator)
        {
            var directorGo = Track(new GameObject("Test_LandmarkDirector"));
            directorGo.SetActive(false);
            var director = directorGo.AddComponent<LandmarkDirector>();
            SetField(director, "terrainGenerator", generator);
            directorGo.SetActive(true);
            return director;
        }

        private LandmarkDefinition CreateDefinition(string id, GameObject prefab)
        {
            var definition = TrackAsset(ScriptableObject.CreateInstance<LandmarkDefinition>());
            SetField(definition, "landmarkId", id);
            SetField(definition, "landmarkPrefab", prefab);
            return definition;
        }

        private LandmarkRegistry CreateRegistry(params LandmarkDefinition[] definitions)
        {
            var registry = TrackAsset(ScriptableObject.CreateInstance<LandmarkRegistry>());
            SetField(registry, "definitions", new List<LandmarkDefinition>(definitions));
            return registry;
        }

        private LandmarkPresenter CreatePresenter(LandmarkDirector director, LandmarkRegistry registry, bool fallback)
        {
            var presenterGo = Track(new GameObject("Test_LandmarkPresenter"));
            presenterGo.SetActive(false);
            var presenter = presenterGo.AddComponent<LandmarkPresenter>();
            SetField(presenter, "director", director);
            SetField(presenter, "registry", registry);
            SetField(presenter, "enableDevelopmentFallback", fallback);
            presenterGo.SetActive(true);
            return presenter;
        }

        private static int CountSceneObjectsStartingWith(string prefix)
        {
            int count = 0;
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (transform.name.StartsWith(prefix)) count++;
            }

            return count;
        }

        #endregion

        #region Director

        [UnityTest]
        public IEnumerator Director_PublishesLandmarksFromLayout()
        {
            var generator = CreateGenerator(
                MakeLandmark("node_a", "ruin", new Vector3(100f, 0f, 100f)),
                MakeLandmark("node_b", "shrine", new Vector3(200f, 0f, 50f)));
            var director = CreateDirector(generator);
            yield return null;

            Assert.AreEqual(2, director.ActiveLandmarkCount);
        }

        [UnityTest]
        public IEnumerator Director_Queries_NearestAndByRing()
        {
            var generator = CreateGenerator(
                MakeLandmark("node_a", "ruin", new Vector3(100f, 0f, 0f), WorldRing.Nearlands),
                MakeLandmark("node_b", "shrine", new Vector3(300f, 0f, 0f), WorldRing.Farlands));
            var director = CreateDirector(generator);
            yield return null;

            var nearest = director.GetNearest(new Vector3(120f, 0f, 0f));
            Assert.IsNotNull(nearest);
            Assert.AreEqual("ruin", nearest.DefinitionId);

            var farlands = new List<LandmarkIdentity>(director.GetByRing(WorldRing.Farlands));
            Assert.AreEqual(1, farlands.Count);
            Assert.AreEqual("shrine", farlands[0].DefinitionId);
        }

        [UnityTest]
        public IEnumerator Director_Disable_ReleasesAll()
        {
            var generator = CreateGenerator(MakeLandmark("node_a", "ruin", Vector3.zero));
            var director = CreateDirector(generator);
            yield return null;

            int released = 0;
            director.OnLandmarkReleased += _ => released++;

            director.gameObject.SetActive(false);
            yield return null;

            Assert.AreEqual(0, director.ActiveLandmarkCount);
            Assert.AreEqual(1, released, "each landmark must fire exactly one release");
        }

        [UnityTest]
        public IEnumerator Director_ReEnable_Republishes()
        {
            var generator = CreateGenerator(MakeLandmark("node_a", "ruin", Vector3.zero));
            var director = CreateDirector(generator);
            yield return null;

            director.gameObject.SetActive(false);
            yield return null;
            director.gameObject.SetActive(true);
            yield return null;

            Assert.AreEqual(1, director.ActiveLandmarkCount, "re-enable must republish the layout's landmarks");
        }

        [UnityTest]
        public IEnumerator Director_EmptyLandmarks_IsClean()
        {
            var generator = CreateGenerator();
            var director = CreateDirector(generator);
            yield return null;

            Assert.AreEqual(0, director.ActiveLandmarkCount);
            Assert.IsNull(director.GetNearest(Vector3.zero));
        }

        #endregion

        #region Presenter

        [UnityTest]
        public IEnumerator Presenter_InstantiatesPrefabVisual()
        {
            var template = Track(new GameObject("Test_LandmarkPrefab"));
            template.transform.position = new Vector3(-1000f, 0f, -1000f);
            var registry = CreateRegistry(CreateDefinition("ruin", template));

            var generator = CreateGenerator(MakeLandmark("node_a", "ruin", new Vector3(80f, 5f, 40f)));
            var director = CreateDirector(generator);
            var presenter = CreatePresenter(director, registry, fallback: false);
            yield return null;

            Assert.AreEqual(1, presenter.ActiveVisualCount);
            Assert.AreEqual(1, CountSceneObjectsStartingWith("Landmark_ruin_"));
        }

        [UnityTest]
        public IEnumerator Presenter_NoPrefab_FallbackOff_ShowsNothing()
        {
            var registry = CreateRegistry(CreateDefinition("ruin", prefab: null));
            var generator = CreateGenerator(MakeLandmark("node_a", "ruin", Vector3.zero));
            var director = CreateDirector(generator);
            var presenter = CreatePresenter(director, registry, fallback: false);
            yield return null;

            Assert.AreEqual(0, presenter.ActiveVisualCount, "no prefab + fallback off = nothing");
        }

        [UnityTest]
        public IEnumerator Presenter_NoPrefab_FallbackOn_ShowsSilhouette()
        {
            var registry = CreateRegistry(CreateDefinition("ruin", prefab: null));
            var generator = CreateGenerator(MakeLandmark("node_a", "ruin", Vector3.zero));
            var director = CreateDirector(generator);
            var presenter = CreatePresenter(director, registry, fallback: true);
            yield return null;

            Assert.AreEqual(1, presenter.ActiveVisualCount);
            Assert.AreEqual(1, CountSceneObjectsStartingWith("Landmark_DEV_"));
            Assert.IsNotNull(GetField<Material>(presenter, "fallbackMaterial"), "one shared fallback material is owned");
        }

        [UnityTest]
        public IEnumerator Presenter_Disable_ReleasesVisuals_NoResidue()
        {
            var template = Track(new GameObject("Test_LandmarkPrefab"));
            var registry = CreateRegistry(CreateDefinition("ruin", template));
            var generator = CreateGenerator(MakeLandmark("node_a", "ruin", Vector3.zero));
            var director = CreateDirector(generator);
            var presenter = CreatePresenter(director, registry, fallback: false);
            yield return null;

            presenter.gameObject.SetActive(false);
            yield return null;

            Assert.AreEqual(0, presenter.ActiveVisualCount);
            Assert.AreEqual(0, CountSceneObjectsStartingWith("Landmark_ruin_"), "no visual residue after disable");
        }

        [UnityTest]
        public IEnumerator Presenter_ReEnable_NoDuplicate()
        {
            var template = Track(new GameObject("Test_LandmarkPrefab"));
            var registry = CreateRegistry(CreateDefinition("ruin", template));
            var generator = CreateGenerator(MakeLandmark("node_a", "ruin", Vector3.zero));
            var director = CreateDirector(generator);
            var presenter = CreatePresenter(director, registry, fallback: false);
            yield return null;

            presenter.gameObject.SetActive(false);
            yield return null;
            presenter.gameObject.SetActive(true);
            yield return null;

            Assert.AreEqual(1, presenter.ActiveVisualCount, "re-enable must not duplicate the visual");
            Assert.AreEqual(1, CountSceneObjectsStartingWith("Landmark_ruin_"));
        }

        [UnityTest]
        public IEnumerator Presenter_Destroy_NoResidualMaterial()
        {
            var registry = CreateRegistry(CreateDefinition("ruin", prefab: null));
            var generator = CreateGenerator(MakeLandmark("node_a", "ruin", Vector3.zero));
            var director = CreateDirector(generator);
            var presenter = CreatePresenter(director, registry, fallback: true);
            yield return null;

            var material = GetField<Material>(presenter, "fallbackMaterial");
            Assert.IsNotNull(material);

            Object.Destroy(presenter.gameObject);
            yield return null;
            yield return null;

            Assert.AreEqual(0, CountSceneObjectsStartingWith("Landmark_DEV_"), "no silhouette residue");
            Assert.IsTrue(material == null, "the presenter destroys its own runtime material");
        }

        #endregion
    }
}
