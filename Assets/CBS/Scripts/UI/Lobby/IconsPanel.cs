using CBS.Scriptable;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CBS.UI
{
    public class IconsPanel : MonoBehaviour
    {
        [SerializeField]
        private string GameScene;

        public void ShowStore()
        {
            var prefabs = CBSScriptable.Get<StorePrefabs>();
            var storePrefab = prefabs.StoreWindows;
            UIView.ShowWindow(storePrefab);
        }

        public void ShowInvertory()
        {
            var prefabs = CBSScriptable.Get<InventoryPrefabs>();
            var invertoryPrefab = prefabs.Inventory;
            UIView.ShowWindow(invertoryPrefab);
        }

        public void ShowLootBox()
        {
            var prefabs = CBSScriptable.Get<LootboxPrefabs>();
            var lootBoxPrefab = prefabs.LootBoxes;
            UIView.ShowWindow(lootBoxPrefab);
        }

        public void ShowChat()
        {
            var prefabs = CBSScriptable.Get<ChatPrefabs>();
            var chatPrefab = prefabs.ChatWindow;
            UIView.ShowWindow(chatPrefab);
        }

        public void ShowFriends()
        {
            var prefabs = CBSScriptable.Get<FriendsPrefabs>();
            var friendsPrefab = prefabs.FriendsWindow;
            UIView.ShowWindow(friendsPrefab);
        }

        public void ShowClan()
        {
            var prefabs = CBSScriptable.Get<ClanPrefabs>();
            var windowPrefab = prefabs.WindowLoader;
            UIView.ShowWindow(windowPrefab);
        }

        public void ShowLeaderboards()
        {
            var prefabs = CBSScriptable.Get<LeaderboardPrefabs>();
            var leaderboardsPrefab = prefabs.LeaderboardsWindow;
            UIView.ShowWindow(leaderboardsPrefab);
        }

        public void ShowTournament()
        {

        }

        public void ShowDailyBonus()
        {
            var prefabs = CBSScriptable.Get<CalendarPrefabs>();
            var dailyBonusPrefab = prefabs.CalendarWindow;
            UIView.ShowWindow(dailyBonusPrefab);
        }

        public void ShowRoulette()
        {
            var prefabs = CBSScriptable.Get<RoulettePrefabs>();
            var roulettePrefab = prefabs.RouletteWindow;
            UIView.ShowWindow(roulettePrefab);
        }

        public void ShowMatchmaking()
        {
            var prefabs = CBSScriptable.Get<MatchmakingPrefabs>();
            var matchmakingPrefab = prefabs.MatchmakingWindow;
            UIView.ShowWindow(matchmakingPrefab);
        }

        public void ShowAchievements()
        {
            var prefabs = CBSScriptable.Get<AchievementsPrefabs>();
            var achievementsWindow = prefabs.AchievementsWindow;
            UIView.ShowWindow(achievementsWindow);
        }

        public void ShowDailyTasks()
        {
            var prefabs = CBSScriptable.Get<ProfileTasksPrefabs>();
            var tasksWindow = prefabs.ProfileTasksWindow;
            UIView.ShowWindow(tasksWindow);
        }

        public void ShowForge()
        {
            var prefabs = CBSScriptable.Get<CraftPrefabs>();
            var craftWindow = prefabs.CraftWindow;
            UIView.ShowWindow(craftWindow);
        }

        public void ShowNotification()
        {
            var prefabs = CBSScriptable.Get<NotificationPrefabs>();
            var notificationWindow = prefabs.NotificationWindow;
            UIView.ShowWindow(notificationWindow);
        }

        public void ShowEvents()
        {
            var prefabs = CBSScriptable.Get<EventsPrefabs>();
            var eventsWindow = prefabs.EventWindow;
            UIView.ShowWindow(eventsWindow);
        }

        public void LoadGame()
        {
            SceneManager.LoadScene(GameScene);
        }
    }
}
