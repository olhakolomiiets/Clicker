using CBS.Models;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class BattlePassTabListener : MonoBehaviour
    {
        [SerializeField]
        private ToggleGroup ToggleGroup;

        public event Action<BattlePassTab> OnTabSelected;

        private BattlePassTab[] AllTabs { get; set; }

        public BattlePassTab ActiveTab
        {
            get
            {
                if (AllTabs == null)
                    AllTabs = GetComponentsInChildren<BattlePassTab>();
                return AllTabs.FirstOrDefault(x => x.IsActive) ?? AllTabs.FirstOrDefault();
            }
        }

        private void Awake()
        {
            AllTabs = GetComponentsInChildren<BattlePassTab>();
        }

        private void Start()
        {
            foreach (var tab in AllTabs)
                tab.OnTabSelected += OnToggleSelected;
        }

        private void OnDestroy()
        {
            foreach (var tab in AllTabs)
                tab.OnTabSelected -= OnToggleSelected;
        }

        private void OnDisable()
        {
            ToggleGroup.SetAllTogglesOff();
            var firstTab = AllTabs.FirstOrDefault();
            firstTab.Activate();
        }

        public void HideAll()
        {
            foreach (var tab in AllTabs)
            {
                tab.gameObject.SetActive(false);
            }
        }

        public void SetActiveTitles(BattlePassInstance instance)
        {
            var bankTitle = AllTabs.FirstOrDefault(x => x.TabType == BattlePassTitle.BANK);
            bankTitle?.gameObject.SetActive(instance.BankEnabled);
            var tasksTitle = AllTabs.FirstOrDefault(x => x.TabType == BattlePassTitle.TASKS);
            tasksTitle?.gameObject.SetActive(instance.BankEnabled);

            foreach (var tab in AllTabs)
            {
                if (tab.TabType == BattlePassTitle.LEVELS || tab.TabType == BattlePassTitle.TICKETS)
                {
                    tab.gameObject.SetActive(true);
                }
            }
        }

        private void OnToggleSelected(BattlePassTab tab)
        {
            OnTabSelected?.Invoke(tab);
        }
    }
}