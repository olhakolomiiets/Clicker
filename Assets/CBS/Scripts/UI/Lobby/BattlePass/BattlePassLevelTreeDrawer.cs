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
    public class BattlePassLevelTreeDrawer : MonoBehaviour
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Timer;
        [SerializeField]
        private Text Level;
        [SerializeField]
        private Text Description;
        [SerializeField]
        private BattlePassScroller Scroller;

        public Action ReloadRequest { get; set; }

        private string BattlePassID { get; set; }
        private IBattlePass BattlePass { get; set; }
        private bool IsActive { get; set; }
        private DateTime? EndDate;
        private BattlePassPrefabs PassPrefabs { get; set; }
        private BattlePassInstance PassInstance { get; set; }

        private void Awake()
        {
            BattlePass = CBSModule.Get<CBSBattlePassModule>();
            PassPrefabs = CBSScriptable.Get<BattlePassPrefabs>();
        }

        public void Draw(BattlePassInstance instance, List<BattlePassLevelInfo> levelTree, BattlePassUserInfo state)
        {
            CleanUI();
            PassInstance = instance;
            BattlePassID = PassInstance.ID;
            IsActive = state.IsActive;
            EndDate = state.EndDate;
            // draw info
            DisplayName.text = instance.DisplayName;
            Level.text = state.PlayerLevel.ToString();
            Description.text = instance.Description;
            // draw levels
            DrawLevels(levelTree);
        }

        private void DrawLevels(List<BattlePassLevelInfo> levels)
        {
            var levelPrefab = PassPrefabs.LevelDrawer;
            var uiList = Scroller.Spawn(levelPrefab, levels);
            var activeLevel = levels.FirstOrDefault(x => x.IsCurrent());
            if (activeLevel != null)
            {
                var activeIndex = activeLevel.LevelIndex;
                var maxIndex = activeLevel.MaxIndex;
                var position = (float)activeIndex / (float)maxIndex;
                Scroller.SetScrollPosition(position);
            }
            if (PassInstance.TimeLimitedRewardEnabled)
            {
                var firstLevelWithLimit = uiList.FirstOrDefault(x => x.GetComponent<BattlePassLevelSlot>().LevelInfo.IsRewardLockedByTimer());
                if (firstLevelWithLimit != null)
                {
                    firstLevelWithLimit.GetComponent<BattlePassLevelSlot>().SetTimerActivity(true);
                }
            }
        }

        private void CleanUI()
        {
            DisplayName.text = string.Empty;
            Timer.text = string.Empty;
            Level.text = string.Empty;
            Description.text = string.Empty;
            Scroller.HideAll();
        }

        private void LateUpdate()
        {
            if (EndDate != null)
            {
                Timer.text = BattlePassUtils.GetFrameTimeLabel(EndDate.GetValueOrDefault());
            }
        }

        // button click
        public void Add10Exp()
        {
            if (!IsActive)
                return;
            BattlePass.AddExpirienceToInstance(BattlePassID, 10, OnAddExpirience);
        }

        public void Add150Exp()
        {
            if (!IsActive)
                return;
            BattlePass.AddExpirienceToInstance(BattlePassID, 150, OnAddExpirience);
        }

        // events
        private void OnAddExpirience(CBSAddExpirienceToInstanceResult result)
        {
            if (result.IsSuccess)
            {
                ReloadRequest?.Invoke();
            }
            else
            {
                Debug.LogError(result.Error.Message);
            }
        }
    }
}
