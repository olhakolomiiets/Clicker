using CBS.Models;
using System;

namespace CBS
{
    public interface IClanTasks
    {
        /// <summary>
        /// Get tasks available for current profile clan
        /// </summary>
        /// <param name="result"></param>
        void GetTasksForClan(Action<CBSGetTasksForClanResult> result);

        /// <summary>
        /// Adds a point to an task. For Tasks "OneShot" completes it immediately, for Tasks "Steps" - adds one step
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        void AddTaskPoint(string taskID, Action<CBSModifyClanTaskPointsResult> result);

        /// <summary>
        /// Adds the points you specified to the task. More suitable for Steps task.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        void AddTaskPoints(string taskID, int points, Action<CBSModifyClanTaskPointsResult> result);

        /// <summary>
        /// Reset tasks for current profile clan
        /// </summary>
        /// <param name="result"></param>
        void ResetTasksForClan(Action<CBSGetTasksForClanResult> result);
    }
}
