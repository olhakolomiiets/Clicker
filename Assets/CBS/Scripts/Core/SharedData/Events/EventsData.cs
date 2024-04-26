
using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class EventsData
    {
        public const string ALL_CATEGORY = "ALL";
        public const string UNDEFINED_CATEGORY = "undefined";
        public const int MinDurationInSeconds = 60;
        public const int DefaultDurationInSeconds = 86400;

        public Categories Categories;
        public List<EventMetaData> EventMetaList;

        public Dictionary<string, EventMetaData> GetMetaDataAsDictionary()
        {
            EventMetaList = EventMetaList ?? new List<EventMetaData>();
            return EventMetaList.ToDictionary(x => x.ID, x => x);
        }

        public List<EventMetaData> GetMetaList()
        {
            EventMetaList = EventMetaList ?? new List<EventMetaData>();
            return EventMetaList;
        }

        public EventMetaData GetMetaData(string eventID)
        {
            var eventsDictionary = GetMetaDataAsDictionary();
            if (eventsDictionary.ContainsKey(eventID))
            {
                return eventsDictionary[eventID];
            }
            else
            {
                var newEvent = new EventMetaData
                {
                    ID = eventID
                };
                EventMetaList.Add(newEvent);
                return newEvent;
            }
        }

        public void SetCategories(Categories categories)
        {
            Categories = categories;
        }

        public List<string> GetCategories()
        {
            Categories = Categories ?? new Categories();
            var categoryList = Categories.List ?? new List<string>();
            categoryList = categoryList.ToList();
            categoryList.Insert(0, ALL_CATEGORY);
            return categoryList;
        }

        public List<string> GetCategoriesToSelect()
        {
            Categories = Categories ?? new Categories();
            var categoryList = Categories.List ?? new List<string>();
            categoryList = categoryList.ToList();
            categoryList.Insert(0, UNDEFINED_CATEGORY);
            return categoryList;
        }

        public void RemoveEvent(string eventID)
        {
            EventMetaList = EventMetaList ?? new List<EventMetaData>();
            var eventObj = EventMetaList.FirstOrDefault(x => x.ID == eventID);
            if (eventObj == null)
                return;
            EventMetaList.Remove(eventObj);
            EventMetaList.TrimExcess();
        }

        public void ApplyMetaData(EventMetaData metaData)
        {
            var eventID = metaData.ID;
            if (string.IsNullOrEmpty(eventID))
                return;
            var oldData = EventMetaList.FirstOrDefault(x => x.ID == eventID);
            if (oldData == null)
                return;
            var index = EventMetaList.IndexOf(oldData);
            EventMetaList[index] = oldData;
        }
    }
}
