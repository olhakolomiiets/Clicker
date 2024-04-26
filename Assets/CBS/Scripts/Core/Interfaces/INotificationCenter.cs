

using CBS.Models;
using System;

namespace CBS
{
    public interface INotificationCenter
    {
        /// <summary>
        /// Notifies when a user has received a reward
        /// </summary>
        event Action<GrantRewardResult> OnRewardCollected;
        /// <summary>
        /// Notifies when a user has read a notification
        /// </summary>
        event Action<CBSNotification> OnReadNotification;
        /// <summary>
        /// Notifies when a user has remove a notification
        /// </summary>
        event Action<CBSNotification> OnRemoveNotification;

        /// <summary>
        /// Get all notifications for profile
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void GetNotificationList(CBSGetNotificationsRequest request, Action<CBSGetNotificationsResult> result);

        /// <summary>
        /// Mark notification as read for profile
        /// </summary>
        /// <param name="notificationInstanceID"></param>
        /// <param name="result"></param>
        void MarkNotificationAsRead(string notificationInstanceID, Action<CBSModifyNotificationResult> result);

        /// <summary>
        /// Claim notification reward.
        /// </summary>
        /// <param name="result"></param>
        void ClaimNotificationReward(string notificationInstanceID, Action<CBSClaimNotificationRewardResult> result);

        /// <summary>
        /// Remove notification from profile list
        /// </summary>
        /// <param name="notificationInstanceID"></param>
        /// <param name="result"></param>
        void RemoveNotification(string notificationInstanceID, Action<CBSModifyNotificationResult> result);

        /// <summary>
        /// Get count of not read and not rewarded notifications
        /// </summary>
        /// <param name="notificationInstanceID"></param>
        /// <param name="result"></param>
        void GetNotificationBadge(Action<CBSBadgeResult> result);
        
        /// <summary>
        /// Send notification to profile
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void SendNotificationToProfile(CBSSendNotificationRequest request, Action<CBSSendNotificationResult> result);
    }
}
