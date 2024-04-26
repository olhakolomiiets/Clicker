using CBS.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class NotificationDrawer : MonoBehaviour
    {
        [SerializeField]
        private Text Title;
        [SerializeField]
        private Text Body;
        [SerializeField]
        private RewardDrawer RewardDrawer;
        [SerializeField]
        private GameObject ClaimButtom;
        [SerializeField]
        private GameObject RemoveButtom;

        public Action<CBSNotification> OnModifyNotification;
        public Action<CBSNotification> OnRemoveNotification;

        private INotificationCenter NotificationCenter { get; set; }
        private CBSNotification Notification { get; set; }
        private NotificationSlot NotificationSlot { get; set; }

        private void Awake()
        {
            NotificationCenter = CBSModule.Get<CBSNotificationModule>();
        }

        public void Draw(CBSNotification notification, NotificationSlot slot)
        {
            Notification = notification;
            NotificationSlot = slot;
            Title.text = notification.Title;
            Body.text = notification.Message;
            var hasReward = notification.HasReward && notification.Reward != null && !notification.Reward.IsEmpty();
            ClaimButtom.SetActive(hasReward);
            RemoveButtom.SetActive(notification.ReadAndRewarded());
            if (hasReward)
            {
                var reward = notification.Reward;
                RewardDrawer.Display(reward);
            }
            if (!Notification.Read)
                MarkAsRead();
        }

        private void MarkAsRead()
        {
            var instanceID = Notification.InstanceID;
            NotificationCenter.MarkNotificationAsRead(instanceID, OnReadNotification);
        }

        public void Clear()
        {
            RewardDrawer.Clear();
            ClaimButtom.SetActive(false);
            RemoveButtom.SetActive(false);
            Title.text = string.Empty;
            Body.text = string.Empty;
        }

        public void ClaimHandler()
        {
            var instanceID = Notification.InstanceID;
            NotificationCenter.ClaimNotificationReward(instanceID, OnGrantReward);
        }

        public void RemoveHandler()
        {
            var instanceID = Notification.InstanceID;
            NotificationCenter.RemoveNotification(instanceID, OnRemoveNotificationEvent);
        }

        // events
        private void OnReadNotification(CBSModifyNotificationResult result)
        {
            if (result.IsSuccess)
            {
                var newNotification = result.Notification;
                Draw(newNotification, NotificationSlot);
                OnModifyNotification?.Invoke(newNotification);
            }
        }

        private void OnGrantReward(CBSClaimNotificationRewardResult result)
        {
            if (result.IsSuccess)
            {
                var newNotification = result.Notification;
                Draw(newNotification, NotificationSlot);
                OnModifyNotification?.Invoke(newNotification);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnRemoveNotificationEvent(CBSModifyNotificationResult result)
        {
            if (result.IsSuccess)
            {
                NotificationSlot?.Hide();
                var notification = result.Notification;
                OnRemoveNotification?.Invoke(notification);
                Clear();
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
