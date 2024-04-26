using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class ClanEventContainer
    {
        public List<ClanEvent> Events;

        public void AddEvent(ClanEvent clanEvent)
        {
            if (Events == null)
                Events = new List<ClanEvent>();
            Events.Add(clanEvent);
        }

        public void RemoveEvent(ClanEvent clanEvent)
        {
            if (Events == null)
                return;
            Events.Remove(clanEvent);
            Events.TrimExcess();
        }

        public bool IsEmpty()
        {
            return Events == null || Events.Count == 0;
        }

        public ClanEventContainer Merge(ClanEventContainer eventContainer)
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
