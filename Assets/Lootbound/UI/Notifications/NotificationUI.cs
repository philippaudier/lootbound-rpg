using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Lootbound.Gameplay.Inventory;

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
            }
        }

        private void OnDisable()
        {
            if (playerInventory != null)
            {
                playerInventory.OnItemAdded -= HandleItemAdded;
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
    }
}
