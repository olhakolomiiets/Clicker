using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ClanTasksScreen : MonoBehaviour, IClanScreen
    {
        [SerializeField]
        private ClanTasksScroller Scroller;
        [SerializeField]
        private Text ResetDateTitle;

        public Action OnBack { get; set; }
        private IClanTasks TaskModule { get; set; }
        private ClanPrefabs Prefabs { get; set; }
        private DateTime? ResetDate { get; set; }

        private void Awake()
        {
            TaskModule = CBSModule.Get<CBSClanModule>();
            Prefabs = CBSScriptable.Get<ClanPrefabs>();
        }

        private void LateUpdate()
        {
            if (ResetDate != null)
            {
                DisplayResetDate();
            }
        }

        public void Hide()
        {
            ResetDate = null;
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            ResetDateTitle.text = string.Empty;
            TaskModule.GetTasksForClan(OnGetTasks);
        }

        private void OnGetTasks(CBSGetTasksForClanResult result)
        {
            if (result.IsSuccess)
            {
                Scroller.HideAll();
                var tasks = result.Tasks;
                var taskUIPrefab = Prefabs.ClanTask;
                ResetDate = result.ResetDate;
                Scroller.Spawn(taskUIPrefab, tasks);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void DisplayResetDate()
        {
            if (ResetDate == null)
                return;
            ResetDateTitle.text = TasksTXTHandler.GetNextResetUTCDateNotification((DateTime)ResetDate);
        }

        public void BackHandler()
        {
            OnBack?.Invoke();
        }

        public void ResetTasksHandler()
        {
            TaskModule.ResetTasksForClan(OnGetTasks);
        }
    }
}
