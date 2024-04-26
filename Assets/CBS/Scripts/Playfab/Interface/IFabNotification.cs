using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public interface IFabNotification
    {
        void GetNotifications(string profileID, int maxCount, string category, NotificationRequest query, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void ReadNotification(string profileID, string notificationInstanceID, Action<ExecuteFunctionResult> onCraft, Action<PlayFabError> onFailed);

        void ClaimNotificationReward(string profileID, string notificationInstanceID, Action<ExecuteFunctionResult> onClain, Action<PlayFabError> onFailed);

        void RemoveNotification(string profileID, string notificationInstanceID, Action<ExecuteFunctionResult> onClain, Action<PlayFabError> onFailed);

        void GetNotificationBadge(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void SendNotificationToProfile(string profileID, string toProfileID, ProfileNotificationTemplate template, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
    }
}
