using System;
using UnityEngine;

namespace Lootbound.Core.Scenes
{
    /// <summary>
    /// Represents a single entry in the development scene catalog.
    /// Used to configure which scenes appear in the development menu.
    /// </summary>
    [Serializable]
    public sealed class DevelopmentSceneEntry
    {
        [Tooltip("Name displayed in the menu.")]
        [SerializeField] private string displayName;

        [Tooltip("Exact scene name (must match Build Settings).")]
        [SerializeField] private string sceneName;

        [Tooltip("Description shown when scene is selected.")]
        [SerializeField, TextArea(2, 4)] private string description;

        [Tooltip("Whether this entry is visible in the menu.")]
        [SerializeField] private bool visible = true;

        /// <summary>
        /// Name displayed in the menu.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Exact scene name as it appears in Build Settings.
        /// </summary>
        public string SceneName => sceneName;

        /// <summary>
        /// Description shown when scene is selected.
        /// </summary>
        public string Description => description;

        /// <summary>
        /// Whether this entry should appear in the menu.
        /// </summary>
        public bool Visible => visible;

        /// <summary>
        /// Checks if the entry has valid data.
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(sceneName);

        /// <summary>
        /// Creates a new entry (for testing).
        /// </summary>
        public DevelopmentSceneEntry(string displayName, string sceneName, string description, bool visible = true)
        {
            this.displayName = displayName;
            this.sceneName = sceneName;
            this.description = description;
            this.visible = visible;
        }

        /// <summary>
        /// Default constructor for serialization.
        /// </summary>
        public DevelopmentSceneEntry() { }
    }
}
