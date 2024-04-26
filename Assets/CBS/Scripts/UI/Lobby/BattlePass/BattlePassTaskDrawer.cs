using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class BattlePassTaskDrawer : MonoBehaviour
    {
        [SerializeField]
        private BattlePassTasksScroller Scroller;
        [SerializeField]
        private Text NotificationLabel;
        [SerializeField]
        private Text ResetDateLabel;

        private BattlePassPrefabs PassPrefabs { get; set; }
        private string BattlePassID { get; set; }
        private IBattlePass BattlePass { get; set; }
        private DateTime? ResetDate { get; set; }

        private void Awake()
        {
            PassPrefabs = CBSScriptable.Get<BattlePassPrefabs>();
            BattlePass = CBSModule.Get<CBSBattlePassModule>();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(BattlePassID))
                return;
            BattlePass.GetBattlePassTasksForProfile(BattlePassID, OnGetTasks);
            NotificationLabel.text = string.Empty;
            ResetDateLabel.text = string.Empty;
            Scroller.HideAll();
        }

        private void OnDisable()
        {
            ResetDate = null;
            BattlePassID = string.Empty;
        }

        private void LateUpdate()
        {
            if (ResetDate != null)
            {
                DisplayResetDate();
            }
        }

        public void Draw(BattlePassInstance instance)
        {
            BattlePassID = instance.ID;
        }

        private void DisplayTasks(List<CBSProfileTask> tasks)
        {
            var prefabUI = PassPrefabs.TaskSlot;
            var uiList = Scroller.Spawn(prefabUI, tasks);
            foreach (var ui in uiList)
            {
                ui.GetComponent<BattlePassTaskUI>().SetBattlePassID(BattlePassID);
            }
        }

        private void DisplayResetDate()
        {
            if (ResetDate == null)
                return;
            ResetDateLabel.text = TasksTXTHandler.GetNextResetDateNotification((DateTime)ResetDate);
        }

        // events
        private void OnGetTasks(CBSGetTasksForProfileResult result)
        {
            if (result.IsSuccess)
            {
                var tasks = result.Tasks;
                ResetDate = result.ResetDate;
                DisplayTasks(tasks);
            }
            else
            {
                var cbsError = result.Error;
                var errorCode = cbsError.CBSCode;
                if (errorCode == ErrorCode.TASKS_NOT_AVAILABLE)
                {
                    NotificationLabel.text = BattlePassTXTHandler.TasksNotEnableTitle;
                }
                else
                {
                    new PopupViewer().ShowFabError(cbsError);
                }
            }
        }
    }
}
