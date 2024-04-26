using CBS.Models;
using CBS.Scriptable;
using UnityEngine;

namespace CBS.UI
{
    public class AchievementsWindow : MonoBehaviour
    {
        [SerializeField]
        private AchievementScroller Scroller;
        [SerializeField]
        private AchievementsTabListener TabListener;

        private IAchievements Achievements { get; set; }
        private AchievementsPrefabs Prefabs { get; set; }

        private void Awake()
        {
            Achievements = CBSModule.Get<CBSAchievementsModule>();
            Prefabs = CBSScriptable.Get<AchievementsPrefabs>();
            TabListener.OnTabSelected += OnTabSelected;
        }

        private void OnDestroy()
        {
            TabListener.OnTabSelected -= OnTabSelected;
        }

        private void OnEnable()
        {
            var activeTab = TabListener.ActiveTab;
            GetAchievements(activeTab);
        }

        private void OnTabSelected(TasksState tab)
        {
            GetAchievements(tab);
        }

        private void GetAchievements(TasksState tab)
        {
            Scroller.HideAll();
            if (tab == TasksState.ALL)
                Achievements.GetAchievementsTable(OnGetAchievements);
            else if (tab == TasksState.ACTIVE)
                Achievements.GetActiveAchievementsTable(OnGetAchievements);
            else if (tab == TasksState.COMPLETED)
                Achievements.GetCompletedAchievementsTable(OnGetAchievements);
        }

        private void OnGetAchievements(CBSGetAchievementsTableResult result)
        {
            if (result.IsSuccess)
            {
                var achievements = result.AchievementsData;
                var achievementsList = achievements.Tasks;
                var achievementPrefab = Prefabs.AchievementsSlot;

                Scroller.Spawn(achievementPrefab, achievementsList);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
                gameObject.SetActive(false);
            }
        }
    }
}
