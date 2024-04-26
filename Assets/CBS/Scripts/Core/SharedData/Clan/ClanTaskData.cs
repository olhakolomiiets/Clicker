using CBS.Models;
using System;
using System.Collections.Generic;

namespace CBS
{
    [Serializable]
    public class ClanTaskData : CBSTasksData<CBSClanTask>
    {
        public const string ClanTasksTitlePrefix = "CTP_";
        public const string ClanTasksTablePrefix = "CTP";

        public int DailyTasksCount;
        public DatePeriod UpdatePeriod;
        public TaskRewardBehavior RewardBehavior;

        public List<CBSClanTask> Tasks;

        public override void Add(CBSClanTask task)
        {
            Tasks.Add(task);
        }

        public override List<CBSClanTask> GetTasks()
        {
            return Tasks;
        }

        public override List<CBSClanTask> NewInstance()
        {
            Tasks = new List<CBSClanTask>();
            return Tasks;
        }

        public override void Remove(CBSClanTask task)
        {
            if (Tasks.Contains(task))
            {
                Tasks.Remove(task);
                Tasks.TrimExcess();
            }
        }
    }
}
