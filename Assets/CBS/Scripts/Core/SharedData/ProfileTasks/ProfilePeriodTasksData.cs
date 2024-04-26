using System;
using System.Collections.Generic;

namespace CBS.Models
{
    [Serializable]
    public class ProfilePeriodTasksData : CBSTasksData<CBSProfileTask>
    {
        public int DailyTasksCount;
        public DatePeriod UpdatePeriod;

        public List<CBSProfileTask> Tasks;

        public override void Add(CBSProfileTask task)
        {
            Tasks.Add(task);
        }

        public override List<CBSProfileTask> GetTasks()
        {
            return Tasks;
        }

        public override List<CBSProfileTask> NewInstance()
        {
            Tasks = new List<CBSProfileTask>();
            return Tasks;
        }

        public override void Remove(CBSProfileTask task)
        {
            if (Tasks.Contains(task))
            {
                Tasks.Remove(task);
                Tasks.TrimExcess();
            }
        }
    }
}
