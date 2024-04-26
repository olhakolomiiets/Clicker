using CBS.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class LeaderboardTab : MonoBehaviour
    {
        public LeaderboardTabType TabType;
        public LeaderboardView TabView;
        public string StatisticName;
        public string TitleText;
        public int MaxCount;

        public Action<LeaderboardTab> OnTabSelected;

        public bool IsActive => Toggle.isOn;

        private Toggle toggle;
        private Toggle Toggle
        {
            get
            {
                if (toggle == null)
                {
                    toggle = gameObject.GetComponent<Toggle>();
                }
                return toggle;
            }
        }

        private Text TitleLabel { get; set; }

        private void Awake()
        {
            TitleLabel = GetComponentInChildren<Text>();
        }

        private void Start()
        {
            Toggle.onValueChanged.AddListener(OnToggleChanged);
            DrawTab();
        }

        private void OnDestroy()
        {
            Toggle.onValueChanged.RemoveListener(OnToggleChanged);
        }

        private void DrawTab()
        {
            TitleLabel.text = TitleText;
        }

        // events
        private void OnToggleChanged(bool val)
        {
            if (val)
            {
                OnTabSelected?.Invoke(this);
            }
        }
    }
}
