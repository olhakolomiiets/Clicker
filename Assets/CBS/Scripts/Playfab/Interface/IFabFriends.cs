using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public interface IFabFriends
    {
        void GetFriendsList(string profileID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetRequestedFriendsList(string profileID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void SendFriendsRequest(string profileID, string friendUserId, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed);

        void RemoveFriend(string profileID, string friendUserId, Action<ExecuteFunctionResult> onRemove, Action<PlayFabError> onFailed);

        void AcceptFriend(string profileID, string friendUserId, Action<ExecuteFunctionResult> onAccept, Action<PlayFabError> onFailed);

        void ForceAddFriend(string profileID, string friendUserId, Action<ExecuteFunctionResult> onAccept, Action<PlayFabError> onFailed);

        void CheckFriendship(string profileID, string friendProfileID, Action<ExecuteFunctionResult> onCheck, Action<PlayFabError> onFailed);

        void GetFriendsBadge(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void DeclineFriend(string profileID, string friendProfileID, Action<ExecuteFunctionResult> onDecline, Action<PlayFabError> onFailed);

        void GetSharedFriends(string profileID, string withProfileID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
    }
}
