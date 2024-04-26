using CBS.Models;
using UnityEngine;

namespace CBS.UI
{
    public class BattlePassWindow : MonoBehaviour
    {
        [SerializeField]
        private BattlePassTabListener TabListener;
        [SerializeField]
        private BattlePassLevelTreeDrawer LevelDrawer;
        [SerializeField]
        private BattlePassBankDrawer BankDrawer;
        [SerializeField]
        private BattlePassTaskDrawer TasksDrawer;
        [SerializeField]
        private BattlePassTicketsDrawer TicketsDrawer;

        private string BattlePassID { get; set; }
        private IBattlePass BattlePass { get; set; }
        private bool IsLoaded { get; set; }
        private BattlePassInstance PassInstance { get; set; }

        private void Awake()
        {
            BattlePass = CBSModule.Get<CBSBattlePassModule>();
            TabListener.OnTabSelected += OnChangeTab;
            LevelDrawer.ReloadRequest += OnReload;
            TicketsDrawer.ReloadRequest += OnReload;
        }

        private void OnDestroy()
        {
            TabListener.OnTabSelected -= OnChangeTab;
            LevelDrawer.ReloadRequest -= OnReload;
            TicketsDrawer.ReloadRequest -= OnReload;
        }

        private void OnDisable()
        {
            IsLoaded = false;
            EnableTitle(BattlePassTitle.NONE);
            PassInstance = null;
        }

        public void Load(string battlePassID)
        {
            BattlePassID = battlePassID;
            BattlePass.GetBattlePassFullInformation(BattlePassID, OnGetBattlePassInfo);
            if (!IsLoaded)
            {
                TabListener.HideAll();
                EnableTitle(BattlePassTitle.NONE);
            }
        }

        private void EnableAllTitle()
        {
            EnableTitle(BattlePassTitle.BANK);
            EnableTitle(BattlePassTitle.TICKETS);
            EnableTitle(BattlePassTitle.TASKS);
            EnableTitle(BattlePassTitle.LEVELS);
        }

        private void EnableTitle(BattlePassTitle title)
        {
            LevelDrawer.gameObject.SetActive(title == BattlePassTitle.LEVELS);
            BankDrawer.gameObject.SetActive(title == BattlePassTitle.BANK && PassInstance != null && PassInstance.BankEnabled);
            TasksDrawer.gameObject.SetActive(title == BattlePassTitle.TASKS && PassInstance != null && PassInstance.TasksEnabled);
            TicketsDrawer.gameObject.SetActive(title == BattlePassTitle.TICKETS);
        }

        // button click
        public void CloseWindow()
        {
            gameObject.SetActive(false);
        }

        // events
        private void OnGetBattlePassInfo(CBSGetBattlePassFullInformationResult result)
        {
            if (result.IsSuccess)
            {
                IsLoaded = true;
                PassInstance = result.Instance;
                var playerState = result.ProfileState;
                TabListener.SetActiveTitles(PassInstance);
                // draw levels
                EnableAllTitle();
                var levels = result.GetLevelTreeDetailList();
                var bankLevels = result.GetBankLevelTree();
                LevelDrawer.Draw(PassInstance, levels, playerState);
                BankDrawer.Draw(bankLevels);
                TasksDrawer.Draw(PassInstance);
                TicketsDrawer.Draw(PassInstance, playerState);
                EnableTitle(BattlePassTitle.NONE);

                OnChangeTab(TabListener.ActiveTab);
            }
        }

        private void OnChangeTab(BattlePassTab tab)
        {
            if (IsLoaded)
            {
                var title = tab.TabType;
                EnableTitle(title);
            }
        }

        private void OnReload()
        {
            Load(BattlePassID);
        }
    }
}
