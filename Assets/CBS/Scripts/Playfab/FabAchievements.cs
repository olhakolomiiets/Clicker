using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabAchievements : FabExecuter, IFabAchievements
    {
        public void GetProfileAchievements(string profileID, TasksState state, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileAchievementsMethod,
                FunctionParameter = new FunctionGetAchievementsRequest
                {
                    ProfileID = profileID,
                    State = state
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void ModifyAchievementPoint(string profileID, string achievementID, int points, ModifyMethod method, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddAchievementPointsMethod,
                FunctionParameter = new FunctionModifyAchievementPointsRequest
                {
                    ProfileID = profileID,
                    Points = points,
                    Method = method,
                    AchievementID = achievementID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onAdd, onFailed);
        }

        public void PickupReward(string profileID, string achievementID, Action<ExecuteFunctionResult> onPick, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PickupAchievementRewardMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = achievementID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onPick, onFailed);
        }

        public void ResetAchievement(string profileID, string achievementID, Action<ExecuteFunctionResult> onReset, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ResetAchievementMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = achievementID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onReset, onFailed);
        }

        public void GetAchievementsBadge(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetAchievementsBadgeMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }
    }
}
