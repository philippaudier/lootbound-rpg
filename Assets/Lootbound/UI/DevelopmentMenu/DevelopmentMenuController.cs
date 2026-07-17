using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Lootbound.Core.Logging;
using Lootbound.Core.Scenes;
using Lootbound.Gameplay.Player;

namespace Lootbound.UI
{
    /// <summary>
    /// Controls the development scene menu.
    /// Provides scene selection from boot and pause menu from gameplay scenes.
    /// </summary>
    public sealed class DevelopmentMenuController : MonoBehaviour
    {
        private const string Category = "DevMenu";

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private DevelopmentSceneCatalog catalog;

        [Header("Settings")]
        [SerializeField] private int sortOrder = 200;

        public enum View { None, Pause, SceneSelection }
        private View currentView = View.None;
        private float savedTimeScale = 1f;
        private bool isFromBoot;
        private string selectedSceneName;

        // UI Elements
        private VisualElement root;
        private VisualElement menuRoot;
        private VisualElement pauseView;
        private VisualElement selectionView;
        private ScrollView sceneList;
        private Label sceneDescription;
        private Button backButton;

        // Dynamic references (resolved after scene load)
        private InventoryUI inventoryUI;
        private RepairStationUI repairStationUI;
        private PlayerCameraController cameraController;

        // Scene button tracking
        private readonly List<Button> sceneButtons = new();

        /// <summary>
        /// True if any menu view is open.
        /// </summary>
        public bool IsOpen => currentView != View.None;

        /// <summary>
        /// True if the pause view is active.
        /// </summary>
        public bool IsPaused => currentView == View.Pause;

        /// <summary>
        /// The current menu view.
        /// </summary>
        public View CurrentView => currentView;

        private void Awake()
        {
            if (uiDocument == null)
            {
                LootboundLog.Error(Category, "UIDocument is not assigned!");
                return;
            }

            SetupUI();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromCameraController();
        }

        private void Start()
        {
            // Detect current scene context
            isFromBoot = (SceneManager.GetActiveScene().name == "00_Boot");

            // Populate scene list if catalog is assigned via inspector
            if (catalog != null)
            {
                PopulateSceneList();
            }

            // Auto-open scene selection when starting from boot scene
            if (isFromBoot)
            {
                OpenSceneSelection();
            }
        }

        /// <summary>
        /// Initialize the menu with a catalog (can be called externally if needed).
        /// </summary>
        public void Initialize(DevelopmentSceneCatalog sceneCatalog)
        {
            catalog = sceneCatalog;
            PopulateSceneList();
        }

        private void SetupUI()
        {
            root = uiDocument.rootVisualElement;
            uiDocument.sortingOrder = sortOrder;

            menuRoot = root.Q<VisualElement>("menu-root");
            pauseView = root.Q<VisualElement>("pause-view");
            selectionView = root.Q<VisualElement>("selection-view");
            sceneList = root.Q<ScrollView>("scene-list");
            sceneDescription = root.Q<Label>("scene-description");
            backButton = root.Q<Button>("back-btn");

            // Pause view buttons
            var continueBtn = root.Q<Button>("continue-btn");
            var reloadBtn = root.Q<Button>("reload-btn");
            var changeSceneBtn = root.Q<Button>("change-scene-btn");
            var returnMenuBtn = root.Q<Button>("return-menu-btn");
            var quitBtn = root.Q<Button>("quit-btn");

            if (continueBtn != null) continueBtn.clicked += OnContinueClicked;
            if (reloadBtn != null) reloadBtn.clicked += OnReloadClicked;
            if (changeSceneBtn != null) changeSceneBtn.clicked += OnChangeSceneClicked;
            if (returnMenuBtn != null) returnMenuBtn.clicked += OnReturnToMenuClicked;
            if (quitBtn != null) quitBtn.clicked += OnQuitClicked;
            if (backButton != null) backButton.clicked += OnBackClicked;

            // Start hidden
            HideAllViews();
        }

        private void PopulateSceneList()
        {
            if (sceneList == null || catalog == null) return;

            // Clear existing buttons
            sceneList.Clear();
            sceneButtons.Clear();

            foreach (var entry in catalog.GetVisibleEntries())
            {
                var button = CreateSceneButton(entry);
                sceneList.Add(button);
                sceneButtons.Add(button);
            }
        }

        private Button CreateSceneButton(DevelopmentSceneEntry entry)
        {
            var button = new Button();
            button.AddToClassList("scene-button");
            button.userData = entry;

            var nameLabel = new Label(entry.DisplayName);
            nameLabel.AddToClassList("scene-button-name");
            button.Add(nameLabel);

            var sceneLabel = new Label(entry.SceneName);
            sceneLabel.AddToClassList("scene-button-scene");
            button.Add(sceneLabel);

            bool canLoad = SceneLoader.CanLoadScene(entry.SceneName);
            if (!canLoad)
            {
                button.AddToClassList("scene-button-unavailable");
                button.SetEnabled(false);
            }

            button.clicked += () => OnSceneButtonClicked(entry);

            return button;
        }

        private void OnSceneButtonClicked(DevelopmentSceneEntry entry)
        {
            if (!SceneLoader.CanLoadScene(entry.SceneName))
            {
                LootboundLog.Warning(Category, $"Scene not available: {entry.SceneName}");
                return;
            }

            selectedSceneName = entry.SceneName;
            UpdateSceneDescription(entry);
            UpdateButtonSelection(entry.SceneName);

            // Load immediately on click
            LoadScene(entry.SceneName);
        }

        private void UpdateSceneDescription(DevelopmentSceneEntry entry)
        {
            if (sceneDescription != null)
            {
                sceneDescription.text = entry?.Description ?? "";
            }
        }

        private void UpdateButtonSelection(string sceneName)
        {
            foreach (var button in sceneButtons)
            {
                var entry = button.userData as DevelopmentSceneEntry;
                if (entry != null && entry.SceneName == sceneName)
                {
                    button.AddToClassList("scene-button-selected");
                }
                else
                {
                    button.RemoveFromClassList("scene-button-selected");
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Unsubscribe from previous scene's camera controller
            UnsubscribeFromCameraController();

            // Resolve dynamic references for the new scene
            inventoryUI = FindFirstObjectByType<InventoryUI>();
            repairStationUI = FindFirstObjectByType<RepairStationUI>();
            cameraController = FindFirstObjectByType<PlayerCameraController>();

            if (cameraController != null)
            {
                cameraController.OnPauseRequested += HandlePauseRequest;
            }

            isFromBoot = (scene.name == "00_Boot");

            LootboundLog.Verbose(Category, $"Scene loaded: {scene.name}, isFromBoot: {isFromBoot}");
        }

        private void UnsubscribeFromCameraController()
        {
            if (cameraController != null)
            {
                cameraController.OnPauseRequested -= HandlePauseRequest;
                cameraController = null;
            }
        }

        private void HandlePauseRequest()
        {
            // Priority: repair station > inventory > dev menu > pause
            if (repairStationUI != null && repairStationUI.IsOpen)
            {
                repairStationUI.Close();
                return;
            }

            if (inventoryUI != null && inventoryUI.IsOpen)
            {
                inventoryUI.Close();
                return;
            }

            if (IsOpen)
            {
                // If in scene selection from a sandbox, go back to pause
                if (currentView == View.SceneSelection && !isFromBoot)
                {
                    ShowPauseView();
                }
                else
                {
                    Close();
                }
            }
            else
            {
                OpenPauseMenu();
            }
        }

        /// <summary>
        /// Open the pause menu (from gameplay).
        /// </summary>
        public void OpenPauseMenu()
        {
            if (SceneLoader.IsLoading) return;

            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            UnlockCursor();
            ShowPauseView();

            LootboundLog.Info(Category, "Pause menu opened");
        }

        /// <summary>
        /// Open the scene selection view (from boot or pause).
        /// </summary>
        public void OpenSceneSelection()
        {
            if (SceneLoader.IsLoading) return;

            // Only pause if coming from gameplay
            if (!isFromBoot && currentView == View.None)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            UnlockCursor();
            ShowSelectionView();

            LootboundLog.Info(Category, "Scene selection opened");
        }

        /// <summary>
        /// Close the menu and resume gameplay.
        /// </summary>
        public void Close()
        {
            if (!IsOpen) return;

            Time.timeScale = savedTimeScale;
            LockCursor();
            HideAllViews();

            LootboundLog.Info(Category, "Menu closed");
        }

        /// <summary>
        /// Load a scene by name.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (SceneLoader.IsLoading)
            {
                LootboundLog.Warning(Category, "Load already in progress");
                return;
            }

            if (!SceneLoader.CanLoadScene(sceneName))
            {
                LootboundLog.Error(Category, $"Cannot load scene: {sceneName}");
                return;
            }

            // Restore time before loading
            Time.timeScale = 1f;
            savedTimeScale = 1f;

            HideAllViews();

            LootboundLog.Info(Category, $"Loading scene: {sceneName}");
            SceneLoader.LoadScene(sceneName);
        }

        /// <summary>
        /// Reload the current scene.
        /// </summary>
        public void ReloadCurrentScene()
        {
            string currentScene = SceneLoader.GetActiveSceneName();

            if (currentScene == "00_Boot")
            {
                LootboundLog.Warning(Category, "Cannot reload boot scene");
                return;
            }

            LoadScene(currentScene);
        }

        private void ShowPauseView()
        {
            currentView = View.Pause;

            if (menuRoot != null) menuRoot.style.display = DisplayStyle.Flex;
            if (pauseView != null) pauseView.style.display = DisplayStyle.Flex;
            if (selectionView != null) selectionView.style.display = DisplayStyle.None;
        }

        private void ShowSelectionView()
        {
            currentView = View.SceneSelection;

            // Refresh the scene list
            PopulateSceneList();

            if (menuRoot != null) menuRoot.style.display = DisplayStyle.Flex;
            if (pauseView != null) pauseView.style.display = DisplayStyle.None;
            if (selectionView != null) selectionView.style.display = DisplayStyle.Flex;

            // Show/hide back button based on context
            if (backButton != null)
            {
                backButton.style.display = isFromBoot ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void HideAllViews()
        {
            currentView = View.None;

            if (menuRoot != null) menuRoot.style.display = DisplayStyle.None;
            if (pauseView != null) pauseView.style.display = DisplayStyle.None;
            if (selectionView != null) selectionView.style.display = DisplayStyle.None;
        }

        private void UnlockCursor()
        {
            // Use PlayerCameraController if available for consistent state
            if (cameraController != null)
            {
                cameraController.UnlockCursor();
            }
            else
            {
                // Fallback for boot scene or missing reference
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
        }

        private void LockCursor()
        {
            // Only lock cursor if we're in a gameplay scene
            if (!isFromBoot)
            {
                // Use PlayerCameraController if available for consistent state
                if (cameraController != null)
                {
                    cameraController.LockCursor();
                }
                else
                {
                    // Fallback
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }

        // Button handlers
        private void OnContinueClicked()
        {
            Close();
        }

        private void OnReloadClicked()
        {
            ReloadCurrentScene();
        }

        private void OnChangeSceneClicked()
        {
            ShowSelectionView();
        }

        private void OnReturnToMenuClicked()
        {
            // Open scene selection, which will allow choosing a new scene
            ShowSelectionView();
        }

        private void OnBackClicked()
        {
            if (isFromBoot)
            {
                // Can't go back from boot
                return;
            }

            ShowPauseView();
        }

        private void OnQuitClicked()
        {
            LootboundLog.Info(Category, "Quit requested");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
