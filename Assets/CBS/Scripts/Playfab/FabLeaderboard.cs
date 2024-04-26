using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabLeaderboard : FabExecuter, IFabLeaderboard
    {
        public void GetLeaderboard(string profileID, CBSGetLeaderboardRequest leaderboardRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetLeaderboardMethod,
                FunctionParameter = new FunctionGetLeaderboardRequest
                {
                    ProfileID = profileID,
                    Constraints = leaderboardRequest.Constraints,
                    MaxCount = leaderboardRequest.MaxCount,
                    StatisticName = leaderboardRequest.StatisticName,
                    Version = leaderboardRequest.Version,
                    StartPostion = leaderboardRequest.StartPosition
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetLeaderboardAroundProfile(string profileID, CBSGetLeaderboardRequest leaderboardRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetLeaderboardAroundProfileMethod,
                FunctionParameter = new FunctionGetLeaderboardRequest
                {
                    ProfileID = profileID,
                    Constraints = leaderboardRequest.Constraints,
                    MaxCount = leaderboardRequest.MaxCount,
                    StatisticName = leaderboardRequest.StatisticName,
                    Version = leaderboardRequest.Version
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetFriendsLeaderboard(string profileID, CBSGetLeaderboardRequest leaderboardRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetFriendsLeaderboardMethod,
                FunctionParameter = new FunctionGetLeaderboardRequest
                {
                    ProfileID = profileID,
                    Constraints = leaderboardRequest.Constraints,
                    MaxCount = leaderboardRequest.MaxCount,
                    StatisticName = leaderboardRequest.StatisticName,
                    Version = leaderboardRequest.Version
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void UpdateStatisticPoint(string profileID, string staticticName, int statisticValue, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateStatisticMethod,
                FunctionParameter = new FunctionUpdateStatisticRequest
                {
                    ProfileID = profileID,
                    StatisticName = staticticName,
                    StatisticValue = statisticValue
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void AddStatisticPoint(string profileID, string staticticName, int statisticValue, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddStatisticMethod,
                FunctionParameter = new FunctionUpdateStatisticRequest
                {
                    ProfileID = profileID,
                    StatisticName = staticticName,
                    StatisticValue = statisticValue
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onAdd, onFailed);
        }

        public void ResetProfileLeaderboards(string profileID, Action<ExecuteFunctionResult> onReset, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ResetProfileStatisticsMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onReset, onFailed);
        }

        // CLANS

        public void AddClanStatisticPoint(string profileID, string clanID, string staticticName, int statisticValue, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddClanStatisticMethod,
                FunctionParameter = new FunctionUpdateClanStatisticRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID,
                    StatisticName = staticticName,
                    StatisticValue = statisticValue
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void UpdateClanStatisticPoint(string profileID, string clanID, string staticticName, int statisticValue, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateClanStatisticMethod,
                FunctionParameter = new FunctionUpdateClanStatisticRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID,
                    StatisticName = staticticName,
                    StatisticValue = statisticValue
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetClanLeaderboard(string clanID, CBSGetClanLeaderboardRequest leaderboardRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanLeaderboardMethod,
                FunctionParameter = new FunctionGetClanLeaderboardRequest
                {
                    ClanID = clanID,
                    Constraints = leaderboardRequest.Constraints,
                    MaxCount = leaderboardRequest.MaxCount,
                    StartPostion = leaderboardRequest.StartPosition,
                    StatisticName = leaderboardRequest.StatisticName,
                    Version = leaderboardRequest.Version
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetLeaderboardAroundClan(string clanID, CBSGetClanLeaderboardRequest leaderboardRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetLeaderboardAroundClanMethod,
                FunctionParameter = new FunctionGetClanLeaderboardRequest
                {
                    ClanID = clanID,
                    Constraints = leaderboardRequest.Constraints,
                    MaxCount = leaderboardRequest.MaxCount,
                    StartPostion = leaderboardRequest.StartPosition,
                    StatisticName = leaderboardRequest.StatisticName,
                    Version = leaderboardRequest.Version
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }
    }
}
