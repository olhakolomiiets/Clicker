using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using System.Collections.Generic;
using System.Linq;
using System;

namespace CBS
{
    public class NotificationModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.GetNotificationsMethod)]
        public static async Task<dynamic> GetNotificationsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetNotificationsRequest>();
            var profileID = request.ProfileID;
            var query = request.Request;
            var maxCount = request.MaxCount;
            var category = request.Category;

            var getResult = await GetNotificationsAsync(profileID, query, maxCount, category);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SendNotificationMethod)]
        public static async Task<dynamic> SendNotificationTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionSendNotificationRequest>();
            var profileID = request.ProfileID;
            var notificationID = request.NotificationID;

            var getResult = await SendNotificationAsync(notificationID, profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SendNotificationToProfileMethod)]
        public static async Task<dynamic> SendNotificationToProfileTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionSendNotificationToProfileRequest>();
            var toProfileID = request.ToProfileID;
            var template = request.NotificationTemplate;
            log.LogInformation(JsonConvert.SerializeObject(template));

            var sendResult = await SendNotificationAsync(template, toProfileID);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError(sendResult.Error).AsFunctionResult();
            }

            return sendResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ReadNotificationMethod)]
        public static async Task<dynamic> ReadNotificationTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var notificationInstanceID = request.ID;

            var readResult = await ReadNotificationAsync(profileID, notificationInstanceID);
            if (readResult.Error != null)
            {
                return ErrorHandler.ThrowError(readResult.Error).AsFunctionResult();
            }

            return readResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ClaimNotificationRewardMethod)]
        public static async Task<dynamic> ClaimNotificationRewardTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var notificationInstanceID = request.ID;

            var readResult = await ClaimNotificationRewardAsync(profileID, notificationInstanceID);
            if (readResult.Error != null)
            {
                return ErrorHandler.ThrowError(readResult.Error).AsFunctionResult();
            }

            return readResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.RemoveNotificationMethod)]
        public static async Task<dynamic> RemoveNotificationTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var notificationInstanceID = request.ID;

            var removeResult = await RemoveNotificationAsync(profileID, notificationInstanceID);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError(removeResult.Error).AsFunctionResult();
            }

            return removeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetNotificationBadgeMethod)]
        public static async Task<dynamic> GetNotificationBadgeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();
            var profileID = request.ProfileID;

            var getResult = await GetNotificationsBadgeAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionSendNotificationResult>> SendNotificationAsync(CBSNotification notification, string toProfileID)
        {
            var getNotificationDataResult = await GetNotificationDataAsync();
            if (getNotificationDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendNotificationResult>(getNotificationDataResult.Error);
            }
            var notificationsData = getNotificationDataResult.Result;
            // generate instance id
            var instanceID = System.Guid.NewGuid().ToString();
            notification.InstanceID = instanceID;
            // set date creation
            notification.CreatedDate = ServerTimeUTC;

            var ttl = notificationsData.GetTTL();
            var sendResult = await TableNotificationAssistant.SendProfileNotificationAsync(toProfileID, notification, ttl);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendNotificationResult>(sendResult.Error);
            }

            return new ExecuteResult<FunctionSendNotificationResult>
            {
                Result = new FunctionSendNotificationResult
                {
                    Notification = notification
                }
            };
        }

        public static async Task<ExecuteResult<FunctionSendNotificationResult>> SendNotificationAsync(ProfileNotificationTemplate template, string toProfileID)
        {
            var getNotificationDataResult = await GetNotificationDataAsync();
            if (getNotificationDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendNotificationResult>(getNotificationDataResult.Error);
            }
            var notificationsData = getNotificationDataResult.Result;
            var notification = CBSNotification.FromProfileTemplate(template);
            // set date creation
            notification.CreatedDate = ServerTimeUTC;

            var ttl = notificationsData.GetTTL();
            var sendResult = await TableNotificationAssistant.SendProfileNotificationAsync(toProfileID, notification, ttl);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendNotificationResult>(sendResult.Error);
            }

            return new ExecuteResult<FunctionSendNotificationResult>
            {
                Result = new FunctionSendNotificationResult
                {
                    Notification = notification
                }
            };
        }

        public static async Task<ExecuteResult<FunctionSendNotificationResult>> SendNotificationAsync(string notificationID, string toProfileID)
        {
            var getNotificationDataResult = await GetNotificationDataAsync();
            if (getNotificationDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendNotificationResult>(getNotificationDataResult.Error);
            }
            var notificationsData = getNotificationDataResult.Result;
            var notificationDictionary = notificationsData.GetNotificationsAsDictionary();
            if (!notificationDictionary.ContainsKey(notificationID))
            {
                return ErrorHandler.NotificationNotFound<FunctionSendNotificationResult>();
            }
            var notification = notificationDictionary[notificationID];
            // generate instance id
            var instanceID = System.Guid.NewGuid().ToString();
            notification.InstanceID = instanceID;
            // set date creation
            notification.CreatedDate = ServerTimeUTC;
            // set target
            var target = string.IsNullOrEmpty(toProfileID) ? NotificationTarget.GLOBAL : NotificationTarget.PROFILE;
            notification.Target = target;

            var ttl = notificationsData.GetTTL();
            var sendResult = target == NotificationTarget.PROFILE ? await TableNotificationAssistant.SendProfileNotificationAsync(toProfileID, notification, ttl) : 
                await TableNotificationAssistant.SendGlobalNotificationAsync(notification, ttl);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendNotificationResult>(sendResult.Error);
            }

            return new ExecuteResult<FunctionSendNotificationResult>
            {
                Result = new FunctionSendNotificationResult
                {
                    Notification = notification
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetNotificationsResult>> GetNotificationsAsync(string profileID, NotificationRequest request, int maxCount, string category)
        {
            var getNotificationDataResult = await GetNotificationDataAsync();
            if (getNotificationDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetNotificationsResult>(getNotificationDataResult.Error);
            }
            var notificationsData = getNotificationDataResult.Result;
            var ttl = notificationsData.GetTTL();
            ExecuteResult<List<CBSNotification>> notificationsResult = new ExecuteResult<List<CBSNotification>>();
            if (request == NotificationRequest.PROFILE)
            {
                var getLastUpdate = await GetLastUpdateAsync(profileID);
                if (getLastUpdate.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGetNotificationsResult>(getLastUpdate.Error);
                }
                var lastUpdate = getLastUpdate.Result.LastUpdateTicks;
                notificationsResult = await TableNotificationAssistant.GetProfileNotificationsAsync(profileID, maxCount, lastUpdate, ttl);
            }       
            else if (request == NotificationRequest.GLOBAL)
                notificationsResult = await TableNotificationAssistant.GetGlobalNotificationsAsync(maxCount, ttl);
            if (notificationsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetNotificationsResult>(notificationsResult.Error);
            }
            var notificationsList = notificationsResult.Result;

            var needToCheckForNewPlayers = notificationsList.Any(x=>x.VisibleForNewPlayer == false) && !string.IsNullOrEmpty(profileID);
            if (needToCheckForNewPlayers)
            {
                var accountInfoResult = await ProfileModule.GetProfileAccountInfoByIdOrName(profileID, null);
                if (accountInfoResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGetNotificationsResult>(accountInfoResult.Error);
                }
                var accountInfo = accountInfoResult.Result;
                var registrationDate = accountInfo.Created.ToUniversalTime();
                var notAvalilableNotifications = new List<CBSNotification>();

                for (int i=0;i<notificationsList.Count; i++)
                {
                    var notification = notificationsList[i];
                    if (!notification.VisibleForNewPlayer)
                    {
                        var notificationDate = notification.CreatedDate.ToUniversalTime();
                        if (DateToTimestamp(registrationDate) > DateToTimestamp(notificationDate))
                        {
                            notAvalilableNotifications.Add(notification);
                        }
                    }
                }
                foreach (var notAvailableNot in notAvalilableNotifications)
                {
                    notificationsList.Remove(notAvailableNot);
                }
            }
            if (!string.IsNullOrEmpty(category))
            {
                notificationsList = notificationsList.Where(x=>x.Category == category).ToList();
            }
            return new ExecuteResult<FunctionGetNotificationsResult>
            {
                Result = new FunctionGetNotificationsResult
                {
                    Notifications = notificationsList
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SaveLastUpdateAsync(string profileID, DateTime lastDate)
        {
            var rawData = lastDate.Ticks.ToString();
            var saveResult = await SaveProfileInternalDataAsync(profileID, ProfileDataKeys.ProfileNotificationUpdate, rawData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionGetNotificationLastUpdateResult>> GetLastUpdateAsync(string profileID)
        {
            var getProfileDataResult = await GetProfileInternalRawData(profileID, ProfileDataKeys.ProfileNotificationUpdate);
            if (getProfileDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetNotificationLastUpdateResult>(getProfileDataResult.Error);
            }
            var rawData = getProfileDataResult.Result;
            if (string.IsNullOrEmpty(rawData))
            {
                return new ExecuteResult<FunctionGetNotificationLastUpdateResult>
                {
                    Result = new FunctionGetNotificationLastUpdateResult
                    {
                        LastUpdateTicks = null
                    }
                };
            }
            else
            {
                long date;
                var result = long.TryParse(rawData,out date);
                if (result)
                {
                    return new ExecuteResult<FunctionGetNotificationLastUpdateResult>
                    {
                        Result = new FunctionGetNotificationLastUpdateResult
                        {
                            LastUpdateTicks = date
                        }
                    };
                }
                else
                {
                    return new ExecuteResult<FunctionGetNotificationLastUpdateResult>
                    {
                        Result = new FunctionGetNotificationLastUpdateResult
                        {
                            LastUpdateTicks = null
                        }
                    };
                }              
            }
        }

        public static async Task<ExecuteResult<FunctionModifyNotificationResult>> ReadNotificationAsync(string profileID, string notificationInstanceID)
        {
            var markResult = await TableNotificationAssistant.MarkNotificationAsReadAsync(profileID, notificationInstanceID);
            if (markResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyNotificationResult>(markResult.Error);
            }
            var getNotificationResult = await TableNotificationAssistant.GetProfileNotificationByInstanceIDAsync(profileID, notificationInstanceID);
            if (getNotificationResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyNotificationResult>(getNotificationResult.Error);
            }
            var notification = getNotificationResult.Result;

            return new ExecuteResult<FunctionModifyNotificationResult>
            {
                Result = new FunctionModifyNotificationResult
                {
                    Notification = notification
                }
            };
        }

        public static async Task<ExecuteResult<FunctionClaimNotificationRewardResult>> ClaimNotificationRewardAsync(string profileID, string notificationInstanceID)
        {
            var getNotificationResult = await TableNotificationAssistant.GetProfileNotificationByInstanceIDAsync(profileID, notificationInstanceID);
            if (getNotificationResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionClaimNotificationRewardResult>(getNotificationResult.Error);
            }
            var notification = getNotificationResult.Result;
            if (notification.Rewarded)
            {
                return ErrorHandler.AlreadyRewarded<FunctionClaimNotificationRewardResult>();
            }
            if (!notification.HasReward)
            {
                return ErrorHandler.RewardNotFound<FunctionClaimNotificationRewardResult>();
            }

            var reward = notification.Reward;

            var grantRewardResult = await RewardModule.GrantRewardToProfileAsync(reward, profileID);
            if (grantRewardResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionClaimNotificationRewardResult>(grantRewardResult.Error);
            }
            var grantResult = grantRewardResult.Result;

            var markResult = await TableNotificationAssistant.MarkNotificationAsRewardedAsync(profileID, notificationInstanceID);
            if (markResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionClaimNotificationRewardResult>(markResult.Error);
            }
            notification.Rewarded = true;

            return new ExecuteResult<FunctionClaimNotificationRewardResult>
            {
                Result = new FunctionClaimNotificationRewardResult
                {
                    RewardResult = grantResult,
                    Notification = notification
                }
            };
        }

        public static async Task<ExecuteResult<FunctionModifyNotificationResult>> RemoveNotificationAsync(string profileID, string notificationInstanceID)
        {
            var getNotificationResult = await TableNotificationAssistant.GetProfileNotificationByInstanceIDAsync(profileID, notificationInstanceID);
            if (getNotificationResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyNotificationResult>(getNotificationResult.Error);
            }
            var notification = getNotificationResult.Result;

            var removeResult = await TableNotificationAssistant.RemoveNotificationAsync(profileID, notificationInstanceID);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyNotificationResult>(removeResult.Error);
            }

            return new ExecuteResult<FunctionModifyNotificationResult>
            {
                Result = new FunctionModifyNotificationResult
                {
                    Notification = notification
                }
            };
        }

        public static async Task<ExecuteResult<FunctionBadgeResult>> GetNotificationsBadgeAsync(string profileID)
        {
            var request = NotificationRequest.PROFILE;
            var maxCount = NotificationsData.MAX_NOTIFICATIONS_LENGTH;
            var GetNotificationResult = await GetNotificationsAsync(profileID, request, maxCount, null);
            if (GetNotificationResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBadgeResult>(GetNotificationResult.Error);
            }
            var notifications = GetNotificationResult.Result.Notifications ?? new List<CBSNotification>();
            var badgeCount = notifications.Where(x=>!x.ReadAndRewarded()).Count();

            return new ExecuteResult<FunctionBadgeResult>
            {
                Result = new FunctionBadgeResult
                {
                    Count = badgeCount
                }
            };
        }

        private static async Task<ExecuteResult<NotificationsData>> GetNotificationDataAsync()
        {
            var dataResult = await GetInternalTitleDataAsObjectAsync<NotificationsData>(TitleKeys.NotificationsDataKey);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<NotificationsData>(dataResult.Error);
            }
            return new ExecuteResult<NotificationsData>
            {
                Result = dataResult.Result ?? new NotificationsData()
            };
        }

        private static async Task<ExecuteResult<CBSNotification>> GetNotificationTemplateByID(string notificationID)
        {
            var notificationDataResult = await GetNotificationDataAsync();
            if (notificationDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<CBSNotification>(notificationDataResult.Error);
            }
            var notificationData = notificationDataResult.Result;
            var notifications = notificationData.GetNotificationsAsDictionary();
            if (!notifications.ContainsKey(notificationID))
            {
                return ErrorHandler.NotificationNotFound<CBSNotification>();
            }
            var notification = notifications[notificationID];

            return new ExecuteResult<CBSNotification>
            {
                Result = notification
            };
        }
    }
}