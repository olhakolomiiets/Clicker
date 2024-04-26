using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSBaseTasksData<T> : CBSTasksData<T> where T : CBSTask
    {
        public List<T> Tasks;

        public override void Add(T task)
        {
            Tasks.Add(task);
        }

        public override List<T> GetTasks()
        {
            return Tasks;
        }

        public override List<T> NewInstance()
        {
            Tasks = new List<T>();
            return Tasks;
        }

        public override void Remove(T task)
        {
            if (Tasks.Contains(task))
            {
                Tasks.Remove(task);
                Tasks.TrimExcess();
            }
        }
    }

}