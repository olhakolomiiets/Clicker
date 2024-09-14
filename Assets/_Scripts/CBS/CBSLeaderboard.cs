using CBS;
using CBS.Models;
using UnityEngine;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

public class CBSLeaderboard : MonoBehaviour
{
    private ILeaderboard Leaderboard { get; set; }

    [SerializeField] private GameRules _gameRules;
    [SerializeField] private CBS.UI.PlayerLeaderboardScroller ProfileScroller;
    [SerializeField] private Text VersionTitle;
    [SerializeField] private Text TimerLabel;
    [SerializeField] private GameObject TimerContainer;

    private DateTime? ResetDate;
    private LeaderboardPrefabs Prefabs { get; set; }
    private IProfile Profile { get; set; }

    private float LastExecuteTime;

    private void Awake()
    {
        Leaderboard = CBSModule.Get<CBSLeaderboardModule>();
        Profile = CBSModule.Get<CBSProfileModule>();
        Prefabs = CBSScriptable.Get<LeaderboardPrefabs>();
    }
    private void OnEnable()
    {
        var statisticName = "Leaderboard";
        var valueToUpdate = (int)_gameRules._totalScore;
        Leaderboard.UpdateStatisticPoint(statisticName, valueToUpdate, OnUpdate);

        Debug.Log("!!!------ CBSLeaderboard: Total Score " + (int)_gameRules._totalScore);      
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

        LoadTab();
    }

    private void LeaderboardRequest()
    {

        var request = new CBSGetLeaderboardRequest
        {
            MaxCount = 100,
            Constraints = CBSProfileConstraints.Default(),
            StatisticName = "Leaderboard",
            StartPosition = 0,
            Version = null
        };
        Leaderboard.GetLeadearboard(request, OnGet);
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

    private void LoadTab()
    {
        ResetTimer();
        ProfileScroller.HideAll();

        var statisticName = "Leaderboard";
        var maxCount = 100;

        LastExecuteTime = Time.time;

        var leaderboardRequest = new CBSGetLeaderboardRequest
        {
            StatisticName = statisticName,
            MaxCount = maxCount
        };

        Leaderboard.GetLeadearboard(leaderboardRequest, OnGetLeaderboard);

            // if (leaderboardView == LeaderboardView.TOP)
            // {
            //     Leaderboard.GetLeadearboard(leaderboardRequest, OnGetLeaderboard);
            // }
            // else
            // {
            //     Leaderboard.GetLeadearboardAround(leaderboardRequest, OnGetLeaderboard);
            // }
    }

    private void LateUpdate()
    {
        if (ResetDate != null)
        {
            TimerLabel.text = LeaderboardTXTHandler.GetNextResetNotification(ResetDate.GetValueOrDefault());
            var timerSize = TimerLabel.rectTransform.sizeDelta;
            timerSize.x = TimerLabel.preferredWidth;
            TimerLabel.rectTransform.sizeDelta = timerSize;
        }
    }

    private void ResetTimer()
    {
        ResetDate = null;
        TimerContainer.SetActive(false);
    }

    // events
    private void OnGetLeaderboard(CBSGetLeaderboardResult result)
    {
        if (result.IsSuccess)
        {
            // draw list
            var entries = result.Leaderboard ?? new List<ProfileLeaderboardEntry>();
            var currentEntry = result.ProfileEntry;
            var entryPrefab = Prefabs.LeaderboardUser;
            // insert current profile if not exist
            var entryExist = entries.Any(x => x.ProfileID == currentEntry.ProfileID);
            if (!entryExist && currentEntry != null)
            {
                entries.Add(currentEntry);
            }
            // draw version
            var version = result.Version;
            VersionTitle.text = LeaderboardTXTHandler.GetVersionText(version);
            // draw timer
            ResetDate = result.NextReset;
            TimerContainer.SetActive(ResetDate != null);

            ProfileScroller.Spawn(entryPrefab, entries);
        }
        else
        {
            new CBS.UI.PopupViewer().ShowFabError(result.Error);
        }
    }

}

