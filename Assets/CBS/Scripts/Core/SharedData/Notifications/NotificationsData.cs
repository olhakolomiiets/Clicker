using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class NotificationsData
    {
        public const string ALL_CATEGORY = "ALL";
        public const string UNDEFINED_CATEGORY = "undefined";
        public const int MAX_NOTIFICATIONS_LENGTH = 100;

        public Categories Categories;
        public List<CBSNotification> NotificationsList;
        public CBSTTL TTL;
        public int? NotificationSecondsTTL;

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

        public void AddNotification(CBSNotification notification)
        {
            NotificationsList = NotificationsList ?? new List<CBSNotification>();
            NotificationsList.Add(notification);
        }

        public void RemoveNotification(CBSNotification notification)
        {
            if (NotificationsList == null)
                return;
            NotificationsList.Remove(notification);
        }

        public List<CBSNotification> GetNotifications()
        {
            NotificationsList = NotificationsList ?? new List<CBSNotification>();
            return NotificationsList;
        }

        public Dictionary<string, CBSNotification> GetNotificationsAsDictionary()
        {
            var notifications = GetNotifications();
            return notifications.ToDictionary(x => x.ID, x => x);
        }

        public int GetTTL()
        {
            if (TTL == CBSTTL.NEVER_EXPIRED)
            {
                return -1;
            }
            else
            {
                if (NotificationSecondsTTL == null)
                    return -1;
                return (int)NotificationSecondsTTL;
            }
        }
    }
}

