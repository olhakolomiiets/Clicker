using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;
using UnityEngine;

namespace CBS.Playfab
{
    public class FabNotification : FabExecuter, IFabNotification
    {
        public void GetNotifications(string profileID, int maxCount, string category, NotificationRequest query, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetNotificationsMethod,
                FunctionParameter = new FunctionGetNotificationsRequest
                {
                    ProfileID = profileID,
                    MaxCount = maxCount,
                    Request = query,
                    Category = category
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void ReadNotification(string profileID, string notificationInstanceID, Action<ExecuteFunctionResult> onCraft, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ReadNotificationMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = notificationInstanceID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onCraft, onFailed);
        }

        public void ClaimNotificationReward(string profileID, string notificationInstanceID, Action<ExecuteFunctionResult> onClain, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ClaimNotificationRewardMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = notificationInstanceID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onClain, onFailed);
        }

        public void RemoveNotification(string profileID, string notificationInstanceID, Action<ExecuteFunctionResult> onClain, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.RemoveNotificationMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = notificationInstanceID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onClain, onFailed);
        }

        public void GetNotificationBadge(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetNotificationBadgeMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }
        
        public void SendNotificationToProfile(string profileID, string toProfileID, ProfileNotificationTemplate template, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.SendNotificationToProfileMethod,
                FunctionParameter = new FunctionSendNotificationToProfileRequest
                {
                    ProfileID = profileID,
                    NotificationTemplate = template,
                    ToProfileID = toProfileID
                }
            };
            (request.FunctionParameter as FunctionSendNotificationToProfileRequest)?.NotificationTemplate?.PrepareCustomData();
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }
    }
}
