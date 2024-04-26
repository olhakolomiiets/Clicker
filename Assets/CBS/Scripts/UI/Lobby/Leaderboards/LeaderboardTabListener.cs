using System;
using System.Linq;
using UnityEngine;

namespace CBS.UI
{
    public class LeaderboardTabListener : MonoBehaviour
    {
        public event Action<LeaderboardTab> OnTabSelected;

        private LeaderboardTab[] AllTabs { get; set; }

        public LeaderboardTab ActiveTab
        {
            get
            {
                if (AllTabs == null)
                    AllTabs = GetComponentsInChildren<LeaderboardTab>();
                return AllTabs.FirstOrDefault(x => x.IsActive) ?? AllTabs.FirstOrDefault();
            }
        }


        private void Awake()
        {
            AllTabs = GetComponentsInChildren<LeaderboardTab>();
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

        private void OnToggleSelected(LeaderboardTab tab)
        {
            OnTabSelected?.Invoke(tab);
        }
    }
}