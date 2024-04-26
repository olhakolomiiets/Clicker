using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CBS
{
    public class CBSNotificationModule : CBSModule, INotificationCenter
    {
        /// <summary>
        /// Notifies when a user has received a reward
        /// </summary>
        public event Action<GrantRewardResult> OnRewardCollected;
        /// <summary>
        /// Notifies when a user has read a notification
        /// </summary>
        public event Action<CBSNotification> OnReadNotification;
        /// <summary>
        /// Notifies when a user has remove a notification
        /// </summary>
        public event Action<CBSNotification> OnRemoveNotification;

        private IFabNotification FabNotification { get; set; }
        private IProfile Profile { get; set; }

        protected override void Init()
        {
            Profile = Get<CBSProfileModule>();
            FabNotification = FabExecuter.Get<FabNotification>();
        }

        /// <summary>
        /// Get all notifications for profile
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void GetNotificationList(CBSGetNotificationsRequest request, Action<CBSGetNotificationsResult> result)
        {
            var profileID = Profile.ProfileID;
            var maxCount = NotificationsData.MAX_NOTIFICATIONS_LENGTH;
            var query = request.Request;
            var category = request.SpecificCategory;

            FabNotification.GetNotifications(profileID, maxCount, category, query, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetNotificationsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetNotificationsResult>();
                    var notifications = functionResult.Notifications;

                    result?.Invoke(new CBSGetNotificationsResult
                    {
                        IsSuccess = true,
                        Notifications = notifications
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetNotificationsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Mark notification as read for profile
        /// </summary>
        /// <param name="notificationInstanceID"></param>
        /// <param name="result"></param>
        public void MarkNotificationAsRead(string notificationInstanceID, Action<CBSModifyNotificationResult> result)
        {
            var profileID = Profile.ProfileID;

            FabNotification.ReadNotification(profileID, notificationInstanceID, onRead =>
            {
                var cbsError = onRead.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyNotificationResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onRead.GetResult<FunctionModifyNotificationResult>();
                    var notification = functionResult.Notification;

                    result?.Invoke(new CBSModifyNotificationResult
                    {
                        IsSuccess = true,
                        Notification = notification
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyNotificationResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Claim notification reward.
        /// </summary>
        /// <param name="result"></param>
        public void ClaimNotificationReward(string notificationInstanceID, Action<CBSClaimNotificationRewardResult> result)
        {
            string profileID = Profile.ProfileID;

            FabNotification.ClaimNotificationReward(profileID, notificationInstanceID, onClaim =>
            {
                var cbsError = onClaim.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSClaimNotificationRewardResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onClaim.GetResult<FunctionClaimNotificationRewardResult>();
                    var rewardResult = functionResult.RewardResult;
                    var notification = functionResult.Notification;
                    var reward = rewardResult.OriginReward;

                    if (rewardResult != null && reward != null)
                    {
                        var currencies = rewardResult.GrantedCurrencies;
                        if (currencies != null)
                        {
                            var codes = currencies.Select(x => x.Key).ToArray();
                            Get<CBSCurrencyModule>().ChangeRequest(codes);
                        }

                        var grantedInstances = rewardResult.GrantedInstances;
                        if (grantedInstances != null && grantedInstances.Count > 0)
                        {
                            var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                            Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        }
                    }

                    result?.Invoke(new CBSClaimNotificationRewardResult
                    {
                        IsSuccess = true,
                        RewardResult = rewardResult,
                        Notification = notification
                    });

                    OnRewardCollected?.Invoke(rewardResult);
                }
            }, onError =>
            {
                result?.Invoke(new CBSClaimNotificationRewardResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Remove notification from profile list
        /// </summary>
        /// <param name="notificationInstanceID"></param>
        /// <param name="result"></param>
        public void RemoveNotification(string notificationInstanceID, Action<CBSModifyNotificationResult> result)
        {
            var profileID = Profile.ProfileID;

            FabNotification.RemoveNotification(profileID, notificationInstanceID, onRead =>
            {
                var cbsError = onRead.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyNotificationResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onRead.GetResult<FunctionModifyNotificationResult>();
                    var notification = functionResult.Notification;

                    OnRemoveNotification?.Invoke(notification);

                    result?.Invoke(new CBSModifyNotificationResult
                    {
                        IsSuccess = true,
                        Notification = notification
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyNotificationResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get count of not read and not rewarded notifications
        /// </summary>
        /// <param name="notificationInstanceID"></param>
        /// <param name="result"></param>
        public void GetNotificationBadge(Action<CBSBadgeResult> result)
        {
            var profileID = Profile.ProfileID;

            FabNotification.GetNotificationBadge(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionBadgeResult>();
                    var notificationBadge = functionResult.Count;

                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = true,
                        Count = notificationBadge
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBadgeResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }
        
        /// <summary>
        /// Send notification to profile
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void SendNotificationToProfile(CBSSendNotificationRequest request, Action<CBSSendNotificationResult> result)
        {
            var profileID = Profile.ProfileID;
            var toProfile = request.ToProfileID;
            var template = request.NotificationTemplate;

            FabNotification.SendNotificationToProfile(profileID, toProfile, template, onSent =>
            {
                var cbsError = onSent.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSSendNotificationResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onSent.GetResult<FunctionSendNotificationResult>();
                    var notification = functionResult.Notification;
                    result?.Invoke(new CBSSendNotificationResult
                    {
                        IsSuccess = true,
                        Notification = notification
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSSendNotificationResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }
    }
}
