using CBS.Models;
using System;
using System.Collections.Generic;

namespace CBS
{
    public interface IProfileTasks
    {
        /// <summary>
        /// Notify when player complete task.
        /// </summary>
        event Action<CBSTask> OnCompleteTask;
        /// <summary>
        /// Notify when player complete task tier.
        /// </summary>
        event Action<CBSTask> OnCompleteTaskTier;
        /// <summary>
        /// Notify when profile receive reward for task.
        /// </summary>
        event Action<GrantRewardResult> OnRewardGranted;
        /// <summary>
        /// Notify when player reset tasks and get new
        /// </summary>
        event Action<List<CBSTask>> OnTasksReseted;

        /// <summary>
        /// Get list of all tasks from pool.
        /// </summary>
        /// <param name="result"></param>
        void GetAllTasksFromPool(string tasksPoolID, Action<CBSGetTasksFromPoolResult> result);

        /// <summary>
        /// Get tasks available for profile from pool
        /// </summary>
        /// <param name="result"></param>
        void GetTasksForProfile(string tasksPoolID, Action<CBSGetTasksForProfileResult> result);

        /// <summary>
        /// Adds a points to multiplicity task. For Tasks "OneShot" completes it immediately, for Tasks "Steps" - adds one step
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        void AddMultiplicityTasksPoint(string tasksPoolID, Dictionary<string, int> modifyPair, Action<CBSModifyProfileMultiplyTaskResult> result);

        /// <summary>
        /// Updates points of multiplicity you specified. More suitable for Steps tasks.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        void UpdateMultiplicityTasksPoint(string tasksPoolID, Dictionary<string, int> modifyPair, Action<CBSModifyProfileMultiplyTaskResult> result);

        /// <summary>
        /// Adds a point to an task. For Tasks "OneShot" completes it immediately, for Tasks "Steps" - adds one step
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        void AddTaskPoint(string tasksPoolID, string taskID, Action<CBSModifyProfileTaskPointsResult> result);

        /// <summary>
        /// Adds the points you specified to the task. More suitable for Steps task.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        void AddTaskPoints(string tasksPoolID, string taskID, int points, Action<CBSModifyProfileTaskPointsResult> result);

        /// <summary>
        /// Adds the points you specified to the task. More suitable for Steps task.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        void AddTaskPoints(CBSTask task, int points, Action<CBSModifyProfileTaskPointsResult> result);

        /// <summary>
        /// Updates the task points you specified. More suitable for Steps tasks.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        void UpdateTaskPoint(string tasksPoolID, string taskID, int points, Action<CBSModifyProfileTaskPointsResult> result);

        /// <summary>
        /// Updates the task points you specified. More suitable for Steps tasks.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        void UpdateTaskPoint(CBSTask task, int points, Action<CBSModifyProfileTaskPointsResult> result);

        /// <summary>
        /// Pick up a reward from a completed task if it hasn't been picked up before.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        void PickupTaskReward(string tasksPoolID, string taskID, Action<CBSModifyProfileTaskPointsResult> result);

        /// <summary>
        /// Reset tasks states for pool and get new random tasks.
        /// </summary>
        /// <param name="result"></param>
        void ResetAndGetNewTasks(string tasksPoolID, Action<CBSGetTasksForProfileResult> result);

        /// <summary>
        /// Get the number of completed tasks that did not receive rewards
        /// </summary>
        /// <param name="result"></param>
        void GetTasksBadge(string tasksPoolID, Action<CBSBadgeResult> result);
    }
}
