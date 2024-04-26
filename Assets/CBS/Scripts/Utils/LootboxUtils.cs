using System;

namespace CBS.Utils
{
    public static class LootboxUtils
    {
        public static string GetNextDateNotification(DateTime nextDate)
        {
            var localTime = DateTime.UtcNow;
            var timeSpan = nextDate.Subtract(localTime);
            var timeString = timeSpan.ToString(DateUtils.LootboxTimerFormat);
            return timeString;
        }
    }
}