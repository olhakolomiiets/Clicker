using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;

namespace CBS.UI
{
    public class ProfileTasksBadge : BaseBadge
    {
        private IProfileTasks CBSProfileTasks { get; set; }
        private ProfileTasksPrefabs TasksPrefabs { get; set; }
        private string TasksPoolID { get; set; }

        private void Awake()
        {
            CBSProfileTasks = CBSModule.Get<CBSProfileTasksModule>();
            TasksPrefabs = CBSScriptable.Get<ProfileTasksPrefabs>();
            var windowPrefab = TasksPrefabs.ProfileTasksWindow;
            var tasksWindow = windowPrefab.GetComponent<ProfileTasksWindow>();
            TasksPoolID = tasksWindow.GetPoolID();

            CBSProfileTasks.OnCompleteTask += OnCompleteTask;
            CBSProfileTasks.OnRewardGranted += OnProfileRewarded;
            CBSProfileTasks.OnTasksReseted += OnTaskReseted;
            CBSProfileTasks.OnCompleteTaskTier += OnCompleteTaskTier;

            UpdateCount(0);
        }

        private void OnDestroy()
        {
            CBSProfileTasks.OnCompleteTask -= OnCompleteTask;
            CBSProfileTasks.OnRewardGranted -= OnProfileRewarded;
            CBSProfileTasks.OnTasksReseted -= OnTaskReseted;
            CBSProfileTasks.OnCompleteTaskTier -= OnCompleteTaskTier;
        }

        private void OnEnable()
        {
            GetCompleteTasks();
        }

        private void GetCompleteTasks()
        {
            CBSProfileTasks.GetTasksBadge(TasksPoolID, OnGetPlayerTasks);
        }

        private void OnGetPlayerTasks(CBSBadgeResult result)
        {
            if (result.IsSuccess)
            {
                var badgeCount = result.Count;
                UpdateCount(badgeCount);
            }
        }

        private void OnProfileRewarded(GrantRewardResult reward)
        {
            GetCompleteTasks();
        }

        private void OnCompleteTask(CBSTask result)
        {
            GetCompleteTasks();
        }

        private void OnCompleteTaskTier(CBSTask result)
        {
            GetCompleteTasks();
        }

        private void OnTaskReseted(List<CBSTask> result)
        {
            GetCompleteTasks();
        }
    }
}
