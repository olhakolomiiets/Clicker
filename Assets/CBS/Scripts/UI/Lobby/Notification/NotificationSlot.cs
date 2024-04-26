using CBS.Core;
using CBS.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Button))]
    public class NotificationSlot : MonoBehaviour, IScrollableItem<NotificationSlotRequest>
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text DateTitle;
        [SerializeField]
        private GameObject UnreadDot;
        [SerializeField]
        private GameObject SelectView;

        private Action<NotificationSlot> SelectAction { get; set; }
        private Button Button { get; set; }
        private CBSNotification ActiveNotification { get; set; }

        public CBSNotification Notification { get; private set; }

        private void Start()
        {
            Button = GetComponent<Button>();
            Button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (Button != null)
                Button.onClick.RemoveListener(OnClick);
        }

        private void OnDisable()
        {
            SelectAction = null;
        }

        public void Display(NotificationSlotRequest data)
        {
            SelectAction = data.SelectAction;
            ActiveNotification = data.Active;
            DrawSlot(data.Notification);
        }

        public void DrawSlot(CBSNotification notification)
        {
            Notification = notification;

            DisplayName.text = Notification.Title;
            var isUnread = !Notification.ReadAndRewarded();
            UnreadDot.SetActive(isUnread);
            DateTitle.text = Notification.CreatedDate.ToLocalTime().ToString();
            if (ActiveNotification == null || ActiveNotification.InstanceID != Notification.InstanceID)
            {
                DeSelect();
            }
            else
            {
                Select();
            }
        }

        public void SetAsActive(CBSNotification notification)
        {
            ActiveNotification = notification;
        }

        public void Select()
        {
            SelectView.SetActive(true);
        }

        public void DeSelect()
        {
            SelectView.SetActive(false);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // events
        private void OnClick()
        {
            SelectAction?.Invoke(this);
        }
    }
}
