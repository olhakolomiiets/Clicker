using System.Collections.Generic;

namespace CBS.Models
{
    public class ProfileTasksData
    {
        // profile tasks
        public const string ProfileTasksTitlePrefix = "PTP_";
        public const string ProfileTasksTablePrefix = "ptp";

        public List<string> TasksList;

        public void AddNewID(string id)
        {
            TasksList = TasksList ?? new List<string>();
            TasksList.Add(id);
        }

        public void RemoveID(string id)
        {
            if (TasksList == null)
                return;
            if (TasksList.Contains(id))
            {
                TasksList.Remove(id);
            }
        }

        public bool IsEmpty()
        {
            if (TasksList == null)
                return true;
            return TasksList.Count == 0;
        }
    }
}
