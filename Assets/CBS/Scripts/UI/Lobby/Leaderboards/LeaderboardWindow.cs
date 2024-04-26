using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class LeaderboardWindow : MonoBehaviour
    {
        [SerializeField]
        private LeaderboardTabListener TabListener;
        [SerializeField]
        private PlayerLeaderboardScroller ProfileScroller;
        [SerializeField]
        private ClanLeaderboardScroller ClanScroller;
        [SerializeField]
        private Text VersionTitle;
        [SerializeField]
        private Text TimerLabel;
        [SerializeField]
        private GameObject TimerContainer;

        private DateTime? ResetDate;
        private LeaderboardPrefabs Prefabs { get; set; }
        private ILeaderboard Leaderboard { get; set; }
        private IProfile Profile { get; set; }

        private float LastExecuteTime;

        private void Awake()
        {
            Leaderboard = CBSModule.Get<CBSLeaderboardModule>();
            Profile = CBSModule.Get<CBSProfileModule>();
            Prefabs = CBSScriptable.Get<LeaderboardPrefabs>();
            TabListener.OnTabSelected += OnTabSelected;
        }

        private void OnDestroy()
        {
            TabListener.OnTabSelected -= OnTabSelected;
        }

        private void OnEnable()
        {
            var activeTab = TabListener.ActiveTab;
            LoadTab(activeTab);
        }

        private void OnTabSelected(LeaderboardTab tab)
        {
            LoadTab(tab);
        }

        private void LoadTab(LeaderboardTab tab)
        {
            ResetTimer();
            ProfileScroller.HideAll();
            ClanScroller.HideAll();

            var leaderboardType = tab.TabType;
            var leaderboardView = tab.TabView;
            var statisticName = tab.StatisticName;
            var maxCount = tab.MaxCount;

            ProfileScroller.gameObject.SetActive(leaderboardType != LeaderboardTabType.CLANS);
            ClanScroller.gameObject.SetActive(leaderboardType == LeaderboardTabType.CLANS);

            LastExecuteTime = Time.time;

            var leaderboardRequest = new CBSGetLeaderboardRequest
            {
                StatisticName = statisticName,
                MaxCount = maxCount
            };

            if (leaderboardType == LeaderboardTabType.PLAYERS)
            {
                if (leaderboardView == LeaderboardView.TOP)
                {
                    Leaderboard.GetLeadearboard(leaderboardRequest, OnGetLeaderboard);
                }
                else
                {
                    Leaderboard.GetLeadearboardAround(leaderboardRequest, OnGetLeaderboard);
                }
            }
            else if (leaderboardType == LeaderboardTabType.FRIENDS)
            {
                Leaderboard.GetFriendsLeadearboard(leaderboardRequest, OnGetLeaderboard);
            }
            else if (leaderboardType == LeaderboardTabType.CLANS)
            {
                if (leaderboardView == LeaderboardView.AROUND && Profile.ExistInClan)
                {

                    Leaderboard.GetLeaderboardAroundClan(Profile.ClanID, new CBSGetClanLeaderboardRequest
                    {
                        StatisticName = tab.StatisticName,
                        MaxCount = maxCount

                    }, OnGetClanLeaderboard);
                }
                else
                {
                    Leaderboard.GetClanLeaderboard(new CBSGetClanLeaderboardRequest
                    {
                        StatisticName = tab.StatisticName,
                        MaxCount = maxCount

                    }, OnGetClanLeaderboard);
                }
            }
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
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnGetClanLeaderboard(CBSGetClanLeaderboardResult result)
        {
            if (result.IsSuccess)
            {
                var entries = result.Leaderboard ?? new List<ClanLeaderboardEntry>();
                var currentEntry = result.ClanEntry;
                // insert current profile if not exist
                var entryExist = currentEntry != null && entries.Any(x => x.ClanID == currentEntry.ClanID);
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

                var entryPrefab = Prefabs.LeaderboardClan;
                ClanScroller.Spawn(entryPrefab, entries);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
