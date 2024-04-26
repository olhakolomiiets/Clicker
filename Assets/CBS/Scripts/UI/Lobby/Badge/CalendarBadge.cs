using CBS.Models;

namespace CBS.UI
{
    public class CalendarBadge : BaseBadge
    {
        private ICalendar Calendar { get; set; }

        private void Awake()
        {
            Calendar = CBSModule.Get<CBSCalendarModule>();
        }

        private void OnEnable()
        {
            Calendar.OnRewardCollected += OnRewardCollected;
            Calendar.OnCalendarGranted += OnCalendarGranted;
            Calendar.OnCalendarPurchased += OnCalendarPurchased;
            Calendar.OnCalendarReseted += OnCalendarReseted;
            UpdateCount(0);
            GetCalendarBadge();
        }

        private void OnDisable()
        {
            Calendar.OnRewardCollected -= OnRewardCollected;
            Calendar.OnCalendarGranted -= OnCalendarGranted;
            Calendar.OnCalendarPurchased -= OnCalendarPurchased;
            Calendar.OnCalendarReseted -= OnCalendarReseted;
        }

        private void GetCalendarBadge()
        {
            Calendar.GetCalendarBadge(OnGetBadge);
        }

        // events
        private void OnRewardCollected(GrantRewardResult result)
        {
            GetCalendarBadge();
        }

        private void OnCalendarPurchased(CalendarInstance result)
        {
            GetCalendarBadge();
        }

        private void OnCalendarGranted(CalendarInstance result)
        {
            GetCalendarBadge();
        }

        private void OnCalendarReseted(CalendarInstance result)
        {
            GetCalendarBadge();
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
