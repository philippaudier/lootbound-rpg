using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Lootbound.Gameplay.Equipment;

namespace Lootbound.UI
{
    /// <summary>
    /// Displays equipment condition change notifications using UI Toolkit.
    /// Shows when weapon condition degrades due to wear.
    /// </summary>
    public class ConditionNotificationUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PlayerWeaponWear playerWeaponWear;

        [Header("Settings")]
        [SerializeField] private float notificationDuration = 4f;
        [SerializeField] private int maxNotifications = 3;

        private VisualElement root;
        private VisualElement notificationContainer;

        private readonly Queue<NotificationEntry> activeNotifications = new();

        private struct NotificationEntry
        {
            public VisualElement Element;
            public float ExpireTime;
        }

        private void Awake()
        {
            if (uiDocument == null)
            {
                Debug.LogError("[ConditionNotificationUI] UIDocument is not assigned!");
                return;
            }

            SetupUI();
        }

        private void OnEnable()
        {
            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnConditionChanged += HandleConditionChanged;
            }
        }

        private void OnDisable()
        {
            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnConditionChanged -= HandleConditionChanged;
            }
        }

        private void SetupUI()
        {
            root = uiDocument.rootVisualElement;
            notificationContainer = root.Q<VisualElement>("condition-notification-container");

            if (notificationContainer == null)
            {
                // Create container if not in UXML
                notificationContainer = new VisualElement();
                notificationContainer.name = "condition-notification-container";
                notificationContainer.AddToClassList("condition-notification-container");
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
                    oldest.Element.AddToClassList("condition-notification-fade-out");
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

        private void HandleConditionChanged(WearResult result)
        {
            ShowConditionNotification(result);
        }

        /// <summary>
        /// Show a condition change notification.
        /// </summary>
        public void ShowConditionNotification(WearResult result)
        {
            if (notificationContainer == null || !result.ConditionChanged)
            {
                return;
            }

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
            var notification = CreateNotificationElement(result);
            notificationContainer.Insert(0, notification);

            // Track for expiration
            activeNotifications.Enqueue(new NotificationEntry
            {
                Element = notification,
                ExpireTime = Time.time + notificationDuration
            });
        }

        private VisualElement CreateNotificationElement(WearResult result)
        {
            bool isBroken = IsBrokenNotification(result.ConditionAfter);

            var notification = new VisualElement();
            notification.AddToClassList("condition-notification-item");

            // Apply special class for broken notification
            if (isBroken)
            {
                notification.AddToClassList("condition-notification-broken");
            }

            // Icon placeholder
            var icon = new VisualElement();
            icon.AddToClassList("condition-notification-icon");
            if (isBroken)
            {
                icon.AddToClassList("condition-notification-icon-broken");
            }
            notification.Add(icon);

            // Text container
            var textContainer = new VisualElement();
            textContainer.AddToClassList("condition-notification-text-container");

            // Equipment name
            var nameLabel = new Label(result.EquipmentName);
            nameLabel.AddToClassList("condition-notification-name");
            if (isBroken)
            {
                nameLabel.AddToClassList("condition-notification-name-broken");
            }
            textContainer.Add(nameLabel);

            // Condition message
            string message = GetConditionMessage(result.ConditionAfter);
            var conditionLabel = new Label(message);
            conditionLabel.AddToClassList("condition-notification-condition");

            // Apply condition color
            Color conditionColor = EquipmentConditionHelper.GetConditionColor(result.ConditionAfter);
            conditionLabel.style.color = conditionColor;

            if (isBroken)
            {
                conditionLabel.AddToClassList("condition-notification-condition-broken");
            }

            textContainer.Add(conditionLabel);

            notification.Add(textContainer);

            return notification;
        }

        private string GetConditionMessage(EquipmentCondition condition)
        {
            return condition switch
            {
                EquipmentCondition.Good => "is showing signs of use",
                EquipmentCondition.Worn => "has seen many battles",
                EquipmentCondition.Fragile => "is becoming fragile!",
                EquipmentCondition.Broken => "HAS BROKEN!",
                _ => "condition changed"
            };
        }

        private bool IsBrokenNotification(EquipmentCondition condition)
        {
            return condition == EquipmentCondition.Broken;
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
