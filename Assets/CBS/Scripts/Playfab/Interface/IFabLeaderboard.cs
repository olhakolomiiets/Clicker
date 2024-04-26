using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public interface IFabLeaderboard
    {
        void GetLeaderboard(string profileID, CBSGetLeaderboardRequest leaderboardRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetLeaderboardAroundProfile(string profileID, CBSGetLeaderboardRequest leaderboardRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetFriendsLeaderboard(string profileID, CBSGetLeaderboardRequest leaderboardRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void UpdateStatisticPoint(string profileID, string staticticName, int statisticValue, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void AddStatisticPoint(string profileID, string staticticName, int statisticValue, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed);

        void ResetProfileLeaderboards(string profileID, Action<ExecuteFunctionResult> onReset, Action<PlayFabError> onFailed);

        void AddClanStatisticPoint(string profileID, string clanID, string staticticName, int statisticValue, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void UpdateClanStatisticPoint(string profileID, string clanID, string staticticName, int statisticValue, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetClanLeaderboard(string clanID, CBSGetClanLeaderboardRequest leaderboardRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetLeaderboardAroundClan(string clanID, CBSGetClanLeaderboardRequest leaderboardRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
    }
}
