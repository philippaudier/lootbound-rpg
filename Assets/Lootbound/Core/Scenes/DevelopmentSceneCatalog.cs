using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lootbound.Core.Scenes
{
    /// <summary>
    /// Catalog of development scenes available in the development menu.
    /// This is an explicit, version-controlled list of sandbox scenes.
    /// </summary>
    [CreateAssetMenu(fileName = "DevelopmentSceneCatalog", menuName = "Lootbound/Development Scene Catalog")]
    public sealed class DevelopmentSceneCatalog : ScriptableObject
    {
        [SerializeField] private List<DevelopmentSceneEntry> entries = new();

        /// <summary>
        /// All entries in the catalog.
        /// </summary>
        public IReadOnlyList<DevelopmentSceneEntry> Entries => entries;

        /// <summary>
        /// Gets entries that are visible and valid.
        /// </summary>
        public IEnumerable<DevelopmentSceneEntry> GetVisibleEntries()
        {
            return entries.Where(e => e != null && e.Visible && e.IsValid);
        }

        /// <summary>
        /// Gets entries that are visible, valid, and can be loaded.
        /// </summary>
        public IEnumerable<DevelopmentSceneEntry> GetLoadableEntries()
        {
            return GetVisibleEntries().Where(e => SceneLoader.CanLoadScene(e.SceneName));
        }

        /// <summary>
        /// Checks if a scene name exists in the catalog.
        /// </summary>
        public bool ContainsScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return entries.Any(e => e != null && e.SceneName == sceneName);
        }

        /// <summary>
        /// Gets an entry by scene name.
        /// </summary>
        public DevelopmentSceneEntry GetEntry(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            return entries.FirstOrDefault(e => e != null && e.SceneName == sceneName);
        }

        /// <summary>
        /// Validates the catalog and returns any issues found.
        /// </summary>
        public List<string> Validate()
        {
            var issues = new List<string>();

            if (entries == null || entries.Count == 0)
            {
                issues.Add("Catalog is empty.");
                return issues;
            }

            var seenNames = new HashSet<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry == null)
                {
                    issues.Add($"Entry {i} is null.");
                    continue;
                }

                if (string.IsNullOrEmpty(entry.SceneName))
                {
                    issues.Add($"Entry {i} ({entry.DisplayName ?? "unnamed"}) has empty scene name.");
                    continue;
                }

                if (seenNames.Contains(entry.SceneName))
                {
                    issues.Add($"Duplicate scene name: {entry.SceneName}");
                }
                else
                {
                    seenNames.Add(entry.SceneName);
                }

                if (!Application.CanStreamedLevelBeLoaded(entry.SceneName))
                {
                    issues.Add($"Scene '{entry.SceneName}' is not in Build Settings.");
                }
            }

            return issues;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var issues = Validate();
            foreach (var issue in issues)
            {
                Debug.LogWarning($"[DevelopmentSceneCatalog] {issue}");
            }
        }
#endif
    }
}
