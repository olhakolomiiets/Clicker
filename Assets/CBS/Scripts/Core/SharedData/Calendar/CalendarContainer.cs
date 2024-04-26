using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class CalendarContainer
    {
        public List<CalendarInstance> Instances;

        public void Add(CalendarInstance instance)
        {
            Instances = Instances ?? new List<CalendarInstance>();
            Instances.Add(instance);
        }

        public void Remove(CalendarInstance instance)
        {
            if (Instances == null)
                return;
            Instances.Remove(instance);
            Instances.TrimExcess();
        }

        public CalendarInstance GetInstanceByID(string id)
        {
            if (Instances == null)
                return null;
            return Instances.FirstOrDefault(x => x.ID == id);
        }

        public void CleanUp()
        {
            if (Instances == null)
                return;
            foreach (var instance in Instances)
            {
                instance.CleanUp();
            }
        }

        public bool IsEmpty()
        {
            if (Instances == null || Instances.Count == 0)
                return true;
            return false;
        }
    }
}
