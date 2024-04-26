using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class ProfileEventContainer
    {
        public List<ProfileEvent> Events;

        public void AddEvent(ProfileEvent profileEvent)
        {
            if (Events == null)
                Events = new List<ProfileEvent>();
            Events.Add(profileEvent);
        }

        public void RemoveEvent(ProfileEvent profileEvent)
        {
            if (Events == null)
                return;
            Events.Remove(profileEvent);
            Events.TrimExcess();
        }

        public bool IsEmpty()
        {
            return Events == null || Events.Count == 0;
        }

        public ProfileEventContainer Merge(ProfileEventContainer eventContainer)
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
