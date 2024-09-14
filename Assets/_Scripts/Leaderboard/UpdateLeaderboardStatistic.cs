using CBS;
using CBS.Models;
using UnityEngine;

public class UpdateLeaderboardStatistic : MonoBehaviour
{
    private ILeaderboard LeaderboardModule { get; set; }

    [SerializeField] private GameRules _gameRules;

    private void Start()
    {
        LeaderboardModule = CBSModule.Get<CBSLeaderboardModule>();

        var statisticName = "TotalScore";
        var valueToUpdate = (int)_gameRules._totalScore;
        LeaderboardModule.UpdateStatisticPoint(statisticName, valueToUpdate, OnUpdate);
    }

    private void OnUpdate(CBSUpdateStatisticResult result)
    {
        if (result.IsSuccess)
        {
            var profileID = result.StatisticName;
            var statisitcNAme = result.StatisticName;
            var updatedPosition = result.StatisticPosition;
            var updatedValue = result.StatisticValue;
        }

        LeaderboardRequest();
    }

    private void LeaderboardRequest()
    {

        var request = new CBSGetLeaderboardRequest
        {
            MaxCount = 100,
            Constraints = CBSProfileConstraints.Default(),
            StatisticName = "TotalScore",
            StartPosition = 0,
            Version = null
        };
        LeaderboardModule.GetLeadearboard(request, OnGet);
    }

    private void OnGet(CBSGetLeaderboardResult result)
    {
        if (result.IsSuccess)
        {
            var version = result.Version;
            var nextResetDate = result.NextReset;
            var profileEntry = result.ProfileEntry;
            var playerEntries = result.Leaderboard;
            foreach (var entry in playerEntries)
            {
                var profileID = entry.ProfileID;
                var displayName = entry.DisplayName;
                var avatar = entry.Avatar;
                var clanID = entry.ClanID;
                var clan = entry.ClanEntity;
                var levelInfo = entry.Level;
                var statisitcs = entry.Statistics;

                var statisticPosition = entry.StatisticPosition;
                var statisticValue = entry.StatisticValue;
            }
        }
    }
}
