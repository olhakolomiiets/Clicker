using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabFriends : FabExecuter, IFabFriends
    {
        public void GetFriendsList(string profileID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetFriendsListMethod,
                FunctionParameter = new FunctionGetFriendsListRequest
                {
                    ProfileID = profileID,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetRequestedFriendsList(string profileID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetRequestedFriendsListMethod,
                FunctionParameter = new FunctionGetFriendsListRequest
                {
                    ProfileID = profileID,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void SendFriendsRequest(string profileID, string friendProfileID, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.SendFriendRequestMethod,
                FunctionParameter = new FunctionFriendsRequest
                {
                    ProfileID = profileID,
                    FriendProfileID = friendProfileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onAdd, onFailed);
        }

        public void RemoveFriend(string profileID, string friendProfileID, Action<ExecuteFunctionResult> onRemove, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.RemoveFriendMethod,
                FunctionParameter = new FunctionFriendsRequest
                {
                    ProfileID = profileID,
                    FriendProfileID = friendProfileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onRemove, onFailed);
        }

        public void AcceptFriend(string profileID, string friendProfileID, Action<ExecuteFunctionResult> onAccept, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AcceptFriendRequestMethod,
                FunctionParameter = new FunctionFriendsRequest
                {
                    ProfileID = profileID,
                    FriendProfileID = friendProfileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onAccept, onFailed);
        }

        public void DeclineFriend(string profileID, string friendProfileID, Action<ExecuteFunctionResult> onDecline, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.DeclineFriendRequestMethod,
                FunctionParameter = new FunctionFriendsRequest
                {
                    ProfileID = profileID,
                    FriendProfileID = friendProfileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onDecline, onFailed);
        }

        public void ForceAddFriend(string profileID, string friendProfileID, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ForceAddFriendMethod,
                FunctionParameter = new FunctionFriendsRequest
                {
                    ProfileID = profileID,
                    FriendProfileID = friendProfileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onAdd, onFailed);
        }

        public void CheckFriendship(string profileID, string friendProfileID, Action<ExecuteFunctionResult> onCheck, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.CheckFriendshipMethod,
                FunctionParameter = new FunctionFriendsRequest
                {
                    ProfileID = profileID,
                    FriendProfileID = friendProfileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onCheck, onFailed);
        }

        public void GetFriendsBadge(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetFriendsBadgeMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetSharedFriends(string profileID, string withProfileID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetSharedFriendsMethod,
                FunctionParameter = new FunctionGetSharedFriendsRequest
                {
                    ProfileID = profileID,
                    WithProfileID = withProfileID,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }
    }
}
