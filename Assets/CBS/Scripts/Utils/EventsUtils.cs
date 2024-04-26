using System;

namespace CBS.Utils
{
    public static class EventsUtils
    {
        public static string GetEndDateNotification(DateTime endDate)
        {
            var localTime = DateTime.UtcNow;
            var timeSpan = endDate.Subtract(localTime);
            var timeString = "Event end in " + timeSpan.ToString(DateUtils.EventTimerFormat);
            return timeString;
        }

        public static string GetNextDateNotification(DateTime nextDate)
        {
            var localTime = DateTime.UtcNow;
            var timeSpan = nextDate.Subtract(localTime);
            var timeString = "Event start in " + timeSpan.ToString(DateUtils.EventTimerFormat);
            return timeString;
        }
    }
}
