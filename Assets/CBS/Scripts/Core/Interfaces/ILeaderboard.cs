using CBS.Models;
using System;

namespace CBS
{
    public interface ILeaderboard
    {
        /// <summary>
        /// Get users leaderboard based by player level/xp
        /// </summary>
        /// <param name="result"></param>
        void GetLevelLeadearboard(CBSGetLevelLeaderboardRequest request, Action<CBSGetLeaderboardResult> result);

        /// <summary>
        /// Get friends leaderboard based by player level/xp
        /// </summary>
        /// <param name="result"></param>
        void GetFriendsLeadearboard(CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result);

        /// <summary>
        /// Get users leaderboard based on custom statistics.
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="result"></param>
        void GetLeadearboard(CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result);

        /// <summary>
        /// Get leaderboard by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void GetLeadearboardByProfileID(string profileID, CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result);

        /// <summary>
        /// Update statisitc points by statistic name
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        void UpdateStatisticPoint(string statisticName, int value, Action<CBSUpdateStatisticResult> result);

        /// <summary>
        /// Add statisitc points by statistic name
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        void AddStatisticPoint(string statisticName, int value, Action<CBSUpdateStatisticResult> result);

        /// <summary>
        /// Reset all players statistics, include all leaderboards and player exp/level.
        /// </summary>
        /// <param name="result"></param>
        void ResetAllProfileStatistics(Action<CBSBaseResult> result);

        /// <summary>
        /// Get leaderboard around user based by player level/xp
        /// </summary>
        /// <param name="result"></param>
        void GetLevelLeadearboardAroundProfile(CBSGetLevelLeaderboardRequest request, Action<CBSGetLeaderboardResult> result);

        /// <summary>
        /// Get leaderboard around user based on custom statistics.
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="result"></param>
        void GetLeadearboardAround(CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result);

        /// <summary>
        /// Get leaderboard around players by profile id.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void GetLeadearboardAroundByProfileID(string profileID, CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result);

        /// <summary>
        /// Add clan statistic point of current profile clan
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        void AddClanStatisticPoint(string statisticName, int value, Action<CBSUpdateClanStatisticResult> result);

        /// <summary>
        /// Update clan statistic point of current profile clan
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        void UpdateClanStatisticPoint(string statisticName, int value, Action<CBSUpdateClanStatisticResult> result);

        /// <summary>
        /// Get clans leaderboard
        /// </summary>
        /// <param name="result"></param>
        void GetClanLeaderboard(CBSGetClanLeaderboardRequest request, Action<CBSGetClanLeaderboardResult> result);

        /// <summary>
        /// Get leaderboard around clan
        /// </summary>
        /// <param name="result"></param>
        void GetLeaderboardAroundClan(string clanID, CBSGetClanLeaderboardRequest request, Action<CBSGetClanLeaderboardResult> result);
    }
}
