using CBS.Core;
using CBS.Models;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Toggle))]
    public class CalendarTitle : MonoBehaviour, IScrollableItem<CalendarInstance>
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Notification;
        [SerializeField]
        private Text Badge;
        [SerializeField]
        private GameObject LockObject;
        [SerializeField]
        private GameObject BadgeObject;
        [SerializeField]
        private Image Icon;

        private Toggle Toggle { get; set; }
        private CalendarInstance Title { get; set; }
        private Action<CalendarInstance, CalendarTitle> SelectAction { get; set; }

        private void Awake()
        {
            Toggle = GetComponent<Toggle>();
            Toggle.onValueChanged.AddListener(OnToggleStateChange);
        }

        private void OnDisable()
        {
            Toggle.isOn = false;
        }

        private void OnDestroy()
        {
            Toggle.onValueChanged.RemoveListener(OnToggleStateChange);
        }

        public void Display(CalendarInstance title)
        {
            Title = title;
            DisplayName.text = title.DisplayName;
            var badgeAvailable = title.BadgeCount > 0;
            BadgeObject.SetActive(badgeAvailable);
            Badge.text = title.BadgeCount.ToString();
            LockObject.SetActive(!title.IsAvailable);
            Icon.sprite = title.GetSprite();
            Notification.text = string.Empty;
        }

        public void SetSelectionAction(Action<CalendarInstance, CalendarTitle> action)
        {
            SelectAction = action;
        }

        private void LateUpdate()
        {
            if (Title == null)
                return;
            Notification.text = Title.GetNotification();
        }

        // events
        private void OnToggleStateChange(bool val)
        {
            if (val)
            {
                SelectAction?.Invoke(Title, this);
            }
        }
    }
}
