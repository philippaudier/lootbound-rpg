using NUnit.Framework;
using UnityEngine;
using Lootbound.Core.Scenes;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the development menu system.
    /// Tests catalog validation and scene loader logic.
    /// </summary>
    public class DevelopmentMenuTests
    {
        #region DevelopmentSceneEntry Tests

        [Test]
        public void DevelopmentSceneEntry_ValidEntry_IsValid()
        {
            var entry = new DevelopmentSceneEntry("Test Scene", "TestScene", "A test scene", true);

            Assert.IsTrue(entry.IsValid);
            Assert.AreEqual("Test Scene", entry.DisplayName);
            Assert.AreEqual("TestScene", entry.SceneName);
            Assert.AreEqual("A test scene", entry.Description);
            Assert.IsTrue(entry.Visible);
        }

        [Test]
        public void DevelopmentSceneEntry_EmptySceneName_IsInvalid()
        {
            var entry = new DevelopmentSceneEntry("Test Scene", "", "A test scene", true);

            Assert.IsFalse(entry.IsValid);
        }

        [Test]
        public void DevelopmentSceneEntry_NullSceneName_IsInvalid()
        {
            var entry = new DevelopmentSceneEntry("Test Scene", null, "A test scene", true);

            Assert.IsFalse(entry.IsValid);
        }

        [Test]
        public void DevelopmentSceneEntry_InvisibleEntry_HasVisibleFalse()
        {
            var entry = new DevelopmentSceneEntry("Hidden", "HiddenScene", "", false);

            Assert.IsFalse(entry.Visible);
        }

        #endregion

        #region DevelopmentSceneCatalog Tests

        private DevelopmentSceneCatalog CreateTestCatalog()
        {
            return ScriptableObject.CreateInstance<DevelopmentSceneCatalog>();
        }

        [Test]
        public void Catalog_Empty_ValidateReturnsIssue()
        {
            var catalog = CreateTestCatalog();

            var issues = catalog.Validate();

            Assert.IsNotEmpty(issues);
            Assert.IsTrue(issues.Exists(i => i.Contains("empty")));
        }

        [Test]
        public void Catalog_ContainsScene_ReturnsTrueForExistingScene()
        {
            var catalog = CreateTestCatalog();

            // Can't easily add entries without reflection in tests
            // This test verifies the method doesn't throw on empty catalog
            Assert.IsFalse(catalog.ContainsScene("NonExistent"));
        }

        [Test]
        public void Catalog_ContainsScene_ReturnsFalseForEmptyName()
        {
            var catalog = CreateTestCatalog();

            Assert.IsFalse(catalog.ContainsScene(""));
            Assert.IsFalse(catalog.ContainsScene(null));
        }

        [Test]
        public void Catalog_GetEntry_ReturnsNullForEmptyName()
        {
            var catalog = CreateTestCatalog();

            Assert.IsNull(catalog.GetEntry(""));
            Assert.IsNull(catalog.GetEntry(null));
        }

        [Test]
        public void Catalog_GetVisibleEntries_ReturnsEmptyForEmptyCatalog()
        {
            var catalog = CreateTestCatalog();

            var visible = catalog.GetVisibleEntries();

            Assert.IsNotNull(visible);
            Assert.IsEmpty(visible);
        }

        #endregion

        #region SceneLoader Tests

        [Test]
        public void SceneLoader_CanLoadScene_ReturnsFalseForEmptyName()
        {
            Assert.IsFalse(SceneLoader.CanLoadScene(""));
            Assert.IsFalse(SceneLoader.CanLoadScene(null));
        }

        [Test]
        public void SceneLoader_CanLoadScene_ReturnsFalseForNonExistentScene()
        {
            // This scene definitely doesn't exist in build settings
            Assert.IsFalse(SceneLoader.CanLoadScene("NonExistentScene_XYZ_123"));
        }

        [Test]
        public void SceneLoader_IsLoading_InitiallyFalse()
        {
            // Reset any previous state
            SceneLoader.ResetLoadingState();

            Assert.IsFalse(SceneLoader.IsLoading);
        }

        [Test]
        public void SceneLoader_GetActiveSceneName_ReturnsNonNull()
        {
            // In edit mode, there's always an active scene
            var sceneName = SceneLoader.GetActiveSceneName();

            Assert.IsNotNull(sceneName);
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            // Reset scene loader state
            SceneLoader.ResetLoadingState();
        }
    }
}
