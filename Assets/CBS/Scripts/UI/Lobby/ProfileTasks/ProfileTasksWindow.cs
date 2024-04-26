using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ProfileTasksWindow : MonoBehaviour
    {
        [SerializeField]
        private string TasksPoolID;
        [SerializeField]
        private TasksScroller Scroller;
        [SerializeField]
        private Text ResetDateTitle;

        private IProfileTasks DailyTasks { get; set; }
        private ProfileTasksPrefabs Prefab { get; set; }
        private DateTime? ResetDate { get; set; }

        private void Awake()
        {
            DailyTasks = CBSModule.Get<CBSProfileTasksModule>();
            Prefab = CBSScriptable.Get<ProfileTasksPrefabs>();
        }

        private void OnEnable()
        {
            Scroller.HideAll();
            LoadAndShowDailyTasks();
        }

        private void OnDisable()
        {
            ResetDateTitle.text = string.Empty;
        }

        private void LateUpdate()
        {
            if (ResetDate != null)
            {
                DisplayResetDate();
            }
        }

        public string GetPoolID()
        {
            return TasksPoolID;
        }

        private void LoadAndShowDailyTasks()
        {
            DailyTasks.GetTasksForProfile(TasksPoolID, OnGetDailyTasks);
        }

        private void OnGetDailyTasks(CBSGetTasksForProfileResult result)
        {
            if (result.IsSuccess)
            {
                var tasks = result.Tasks;
                ResetDate = result.ResetDate;
                DisplayTasks(tasks);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
                gameObject.SetActive(false);
            }
        }

        private void DisplayTasks(List<CBSProfileTask> tasks)
        {
            var taskPrefab = Prefab.TaskSlot;
            Scroller.Spawn(taskPrefab, tasks);
        }

        private void DisplayResetDate()
        {
            if (ResetDate == null)
                return;
            ResetDateTitle.text = TasksTXTHandler.GetNextResetDateNotification(ResetDate.GetValueOrDefault());
        }

        // button click events
        public void ResetTasks()
        {
            Scroller.HideAll();
            DailyTasks.ResetAndGetNewTasks(TasksPoolID, onReset =>
            {
                if (onReset.IsSuccess)
                {
                    var newTasks = onReset.Tasks;
                    ResetDate = onReset.ResetDate;
                    DisplayTasks(newTasks);
                }
                else
                {
                    new PopupViewer().ShowFabError(onReset.Error);
                }
            });
        }
    }
}
