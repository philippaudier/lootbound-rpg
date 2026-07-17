using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.Expeditions;

namespace Lootbound.UI
{
    /// <summary>
    /// Displays pickup notifications using UI Toolkit.
    /// Shows a stack of notifications that fade out over time.
    /// </summary>
    public class NotificationUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private InventoryConfig inventoryConfig;

        [Header("Expedition")]
        [SerializeField] private ExpeditionLifecycle expeditionLifecycle;
        [SerializeField] private bool showExpeditionNotifications = true;

        [Header("Settings")]
        [SerializeField] private float notificationDuration = 3f;
        [SerializeField] private int maxNotifications = 5;

        private VisualElement root;
        private VisualElement notificationContainer;

        private Queue<NotificationEntry> activeNotifications = new Queue<NotificationEntry>();

        private struct NotificationEntry
        {
            public VisualElement Element;
            public float ExpireTime;
        }

        private void Awake()
        {
            if (uiDocument == null)
            {
                Debug.LogError("[NotificationUI] UIDocument is not assigned!");
                return;
            }

            SetupUI();

            // Override settings from config if available
            if (inventoryConfig != null)
            {
                notificationDuration = inventoryConfig.NotificationDuration;
                maxNotifications = inventoryConfig.MaxNotifications;
            }
        }

        private void OnEnable()
        {
            if (playerInventory != null)
            {
                playerInventory.OnItemAdded += HandleItemAdded;
                playerInventory.OnEquipmentAdded += HandleEquipmentAdded;
                playerInventory.OnInventoryFull += HandleInventoryFull;
            }

            // Auto-find expedition lifecycle if not set
            if (expeditionLifecycle == null)
            {
                expeditionLifecycle = FindFirstObjectByType<ExpeditionLifecycle>();
            }

            if (expeditionLifecycle != null)
            {
                expeditionLifecycle.OnStateChanged += HandleExpeditionStateChanged;
            }
        }

        private void OnDisable()
        {
            if (playerInventory != null)
            {
                playerInventory.OnItemAdded -= HandleItemAdded;
                playerInventory.OnEquipmentAdded -= HandleEquipmentAdded;
                playerInventory.OnInventoryFull -= HandleInventoryFull;
            }

            if (expeditionLifecycle != null)
            {
                expeditionLifecycle.OnStateChanged -= HandleExpeditionStateChanged;
            }
        }

        private void SetupUI()
        {
            root = uiDocument.rootVisualElement;
            notificationContainer = root.Q<VisualElement>("notification-container");

            if (notificationContainer == null)
            {
                // Create container if not in UXML
                notificationContainer = new VisualElement();
                notificationContainer.name = "notification-container";
                notificationContainer.AddToClassList("notification-container");
                root.Add(notificationContainer);
            }
        }

        private void Update()
        {
            ProcessExpiredNotifications();
        }

        private void ProcessExpiredNotifications()
        {
            float currentTime = Time.time;

            while (activeNotifications.Count > 0)
            {
                var oldest = activeNotifications.Peek();
                if (oldest.ExpireTime <= currentTime)
                {
                    activeNotifications.Dequeue();

                    // Fade out and remove
                    oldest.Element.AddToClassList("notification-fade-out");
                    oldest.Element.RegisterCallback<TransitionEndEvent>(e =>
                    {
                        if (notificationContainer != null && oldest.Element.parent == notificationContainer)
                        {
                            notificationContainer.Remove(oldest.Element);
                        }
                    });
                }
                else
                {
                    break;
                }
            }
        }

        private void HandleItemAdded(ItemDefinition definition, int quantity)
        {
            if (inventoryConfig != null && !inventoryConfig.ShowPickupNotifications)
            {
                return;
            }

            ShowNotification(definition, quantity);
        }

        private void HandleEquipmentAdded(ItemInstance item)
        {
            if (inventoryConfig != null && !inventoryConfig.ShowPickupNotifications)
            {
                return;
            }

            ShowEquipmentNotification(item);
        }

        private void HandleInventoryFull(ItemDefinition definition, int quantity)
        {
            ShowInventoryFullNotification(definition);
        }

        /// <summary>
        /// Show a pickup notification.
        /// </summary>
        public void ShowNotification(ItemDefinition definition, int quantity)
        {
            if (notificationContainer == null || definition == null) return;

            // Remove oldest if at max
            while (activeNotifications.Count >= maxNotifications)
            {
                var oldest = activeNotifications.Dequeue();
                if (oldest.Element.parent == notificationContainer)
                {
                    notificationContainer.Remove(oldest.Element);
                }
            }

            // Create notification element
            var notification = CreateNotificationElement(definition, quantity);
            notificationContainer.Insert(0, notification);

            // Track for expiration
            activeNotifications.Enqueue(new NotificationEntry
            {
                Element = notification,
                ExpireTime = Time.time + notificationDuration
            });
        }

        private VisualElement CreateNotificationElement(ItemDefinition definition, int quantity)
        {
            var notification = new VisualElement();
            notification.AddToClassList("notification-item");

            // Icon
            var icon = new VisualElement();
            icon.AddToClassList("notification-icon");
            if (definition.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(definition.Icon);
            }
            notification.Add(icon);

            // Text container
            var textContainer = new VisualElement();
            textContainer.AddToClassList("notification-text-container");

            // Item name
            var nameLabel = new Label(definition.DisplayName);
            nameLabel.AddToClassList("notification-name");
            nameLabel.style.color = definition.GetRarityColor();
            textContainer.Add(nameLabel);

            // Quantity
            if (quantity > 1)
            {
                var quantityLabel = new Label($"x{quantity}");
                quantityLabel.AddToClassList("notification-quantity");
                textContainer.Add(quantityLabel);
            }

            notification.Add(textContainer);

            return notification;
        }

        /// <summary>
        /// Show a notification for equipment pickup (shows equipment name if custom).
        /// </summary>
        public void ShowEquipmentNotification(ItemInstance item)
        {
            if (notificationContainer == null || item == null) return;

            // Remove oldest if at max
            while (activeNotifications.Count >= maxNotifications)
            {
                var oldest = activeNotifications.Dequeue();
                if (oldest.Element.parent == notificationContainer)
                {
                    notificationContainer.Remove(oldest.Element);
                }
            }

            // Create notification element
            var notification = CreateEquipmentNotificationElement(item);
            notificationContainer.Insert(0, notification);

            // Track for expiration
            activeNotifications.Enqueue(new NotificationEntry
            {
                Element = notification,
                ExpireTime = Time.time + notificationDuration
            });
        }

        /// <summary>
        /// Show a notification that inventory is full.
        /// </summary>
        public void ShowInventoryFullNotification(ItemDefinition definition)
        {
            if (notificationContainer == null) return;

            // Remove oldest if at max
            while (activeNotifications.Count >= maxNotifications)
            {
                var oldest = activeNotifications.Dequeue();
                if (oldest.Element.parent == notificationContainer)
                {
                    notificationContainer.Remove(oldest.Element);
                }
            }

            // Create notification element
            var notification = CreateInventoryFullElement(definition);
            notificationContainer.Insert(0, notification);

            // Track for expiration
            activeNotifications.Enqueue(new NotificationEntry
            {
                Element = notification,
                ExpireTime = Time.time + notificationDuration
            });
        }

        private VisualElement CreateEquipmentNotificationElement(ItemInstance item)
        {
            var notification = new VisualElement();
            notification.AddToClassList("notification-item");

            // Icon
            var icon = new VisualElement();
            icon.AddToClassList("notification-icon");
            if (item.Definition?.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(item.Definition.Icon);
            }
            notification.Add(icon);

            // Text container
            var textContainer = new VisualElement();
            textContainer.AddToClassList("notification-text-container");

            // Equipment name (custom name or definition name)
            string displayName = item.HasEquipmentData && !string.IsNullOrEmpty(item.EquipmentData.CustomName)
                ? item.EquipmentData.CustomName
                : item.Definition?.DisplayName ?? "Equipment";

            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("notification-name");

            // Use equipment's actual rarity for color, not definition's base rarity
            Color rarityColor = item.HasEquipmentData
                ? ItemDefinition.GetColorForRarity(item.EquipmentData.Rarity)
                : item.Definition?.GetRarityColor() ?? Color.white;
            nameLabel.style.color = rarityColor;
            textContainer.Add(nameLabel);

            notification.Add(textContainer);

            return notification;
        }

        private VisualElement CreateInventoryFullElement(ItemDefinition definition)
        {
            var notification = new VisualElement();
            notification.AddToClassList("notification-item");
            notification.AddToClassList("notification-full");

            // Icon
            var icon = new VisualElement();
            icon.AddToClassList("notification-icon");
            if (definition?.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(definition.Icon);
            }
            notification.Add(icon);

            // Text container
            var textContainer = new VisualElement();
            textContainer.AddToClassList("notification-text-container");

            // Item name with "Inventory full" message
            var nameLabel = new Label(definition?.DisplayName ?? "Item");
            nameLabel.AddToClassList("notification-name");
            nameLabel.style.color = new Color(0.8f, 0.4f, 0.4f); // Red tint for warning
            textContainer.Add(nameLabel);

            var fullLabel = new Label("Inventory full");
            fullLabel.AddToClassList("notification-quantity");
            fullLabel.style.color = new Color(0.7f, 0.3f, 0.3f);
            textContainer.Add(fullLabel);

            notification.Add(textContainer);

            return notification;
        }

        /// <summary>
        /// Clear all active notifications.
        /// </summary>
        public void ClearAll()
        {
            if (notificationContainer != null)
            {
                notificationContainer.Clear();
            }
            activeNotifications.Clear();
        }

        #region Expedition Notifications

        private void HandleExpeditionStateChanged(ExpeditionState oldState, ExpeditionState newState)
        {
            if (!showExpeditionNotifications) return;

            // Show notification based on state transition
            switch (newState)
            {
                case ExpeditionState.Active when oldState == ExpeditionState.Departing:
                    ShowTextNotification("Expedition Started", new Color(0.9f, 0.8f, 0.4f)); // Warm yellow
                    break;

                case ExpeditionState.Completed:
                    ShowTextNotification("Safe Return", new Color(0.4f, 0.9f, 0.5f)); // Green
                    break;

                case ExpeditionState.Failed:
                    ShowTextNotification("Expedition Failed", new Color(0.9f, 0.4f, 0.4f)); // Red
                    break;
            }
        }

        /// <summary>
        /// Show a simple text notification without icon.
        /// Used for expedition status messages.
        /// </summary>
        public void ShowTextNotification(string message, Color color)
        {
            if (notificationContainer == null || string.IsNullOrEmpty(message)) return;

            // Remove oldest if at max
            while (activeNotifications.Count >= maxNotifications)
            {
                var oldest = activeNotifications.Dequeue();
                if (oldest.Element.parent == notificationContainer)
                {
                    notificationContainer.Remove(oldest.Element);
                }
            }

            // Create notification element
            var notification = CreateTextNotificationElement(message, color);
            notificationContainer.Insert(0, notification);

            // Track for expiration
            activeNotifications.Enqueue(new NotificationEntry
            {
                Element = notification,
                ExpireTime = Time.time + notificationDuration
            });
        }

        private VisualElement CreateTextNotificationElement(string message, Color color)
        {
            var notification = new VisualElement();
            notification.AddToClassList("notification-item");
            notification.AddToClassList("notification-expedition");

            // Text container (centered, no icon)
            var textContainer = new VisualElement();
            textContainer.AddToClassList("notification-text-container");
            textContainer.style.flexGrow = 1;
            textContainer.style.alignItems = Align.Center;

            // Message label
            var messageLabel = new Label(message);
            messageLabel.AddToClassList("notification-name");
            messageLabel.style.color = color;
            messageLabel.style.fontSize = 14;
            messageLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            textContainer.Add(messageLabel);

            notification.Add(textContainer);

            return notification;
        }

        #endregion
    }
}
