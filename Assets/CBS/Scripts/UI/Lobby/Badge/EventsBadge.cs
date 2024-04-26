using CBS.Models;
using UnityEngine;

namespace CBS.UI
{
    public class EventsBadge : BaseBadge
    {
        [SerializeField]
        private string Category;
        private IEventsModule Events { get; set; }

        private void Awake()
        {
            Events = CBSModule.Get<CBSEventsModule>();
        }

        private void OnEnable()
        {

            UpdateCount(0);
            GetCalendarBadge();
        }

        private void GetCalendarBadge()
        {
            Events.GetEventsBadge(Category, OnGetBadge);
        }

        private void OnGetBadge(CBSBadgeResult result)
        {
            if (result.IsSuccess)
            {
                var badgeCount = result.Count;
                UpdateCount(badgeCount);
            }
        }
    }
}
