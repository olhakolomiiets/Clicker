using CBS.Models;

namespace CBS.UI
{
    public class NotificationBadge : BaseBadge
    {
        private INotificationCenter Notifications { get; set; }
        private ICBSChat Chat { get; set; }

        private int NotificationsCount { get; set; }
        private int DialogsCount { get; set; }

        private void Awake()
        {
            Notifications = CBSModule.Get<CBSNotificationModule>();
            Chat = CBSModule.Get<CBSChatModule>();
        }

        private void OnEnable()
        {
            Notifications.OnRewardCollected += OnRewardCollected;
            Notifications.OnReadNotification += OnReadNotification;
            Chat.OnUnreadMessageClear += OnDialogBadgeChange;
            UpdateCount(0);
            GetNotificationsBadge();
            GetDialogsBadge();
        }

        private void OnDisable()
        {
            Notifications.OnRewardCollected -= OnRewardCollected;
            Notifications.OnReadNotification -= OnReadNotification;
            Chat.OnUnreadMessageClear -= OnDialogBadgeChange;
        }

        private void GetNotificationsBadge()
        {
            Notifications.GetNotificationBadge(OnGetNotificationBadge);
        }

        private void GetDialogsBadge()
        {
            Chat.GetProfileDialogBadge(OnGetDialogBadge);
        }

        // events
        private void OnRewardCollected(GrantRewardResult result)
        {
            GetNotificationsBadge();
        }

        private void OnReadNotification(CBSNotification result)
        {
            GetNotificationsBadge();
        }

        private void OnGetNotificationBadge(CBSBadgeResult result)
        {
            if (result.IsSuccess)
            {
                NotificationsCount = result.Count;
                UpdateCount(NotificationsCount + DialogsCount);
            }
        }

        private void OnGetDialogBadge(CBSBadgeResult result)
        {
            if (result.IsSuccess)
            {
                DialogsCount = result.Count;
                UpdateCount(NotificationsCount + DialogsCount);
            }
        }

        private void OnDialogBadgeChange(ChatDialogEntry dialogEntry)
        {
            GetDialogsBadge();
        }
    }
}
