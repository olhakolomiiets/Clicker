using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class TaskEventContainer
    {
        public List<TaskEvent> Events;

        public void AddEvent(TaskEvent taskEvent)
        {
            if (Events == null)
                Events = new List<TaskEvent>();
            Events.Add(taskEvent);
        }

        public void RemoveEvent(TaskEvent taskEvent)
        {
            if (Events == null)
                return;
            Events.Remove(taskEvent);
            Events.TrimExcess();
        }

        public bool IsEmpty()
        {
            return Events == null || Events.Count == 0;
        }

        public TaskEventContainer Merge(TaskEventContainer eventContainer)
        {
            if (eventContainer == null || eventContainer.Events == null)
                return this;
            if (Events == null)
                return eventContainer;
            Events = Events.Concat(eventContainer.Events).ToList();
            return this;
        }
    }
}
