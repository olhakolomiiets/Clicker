using CBS.Models;
using System;
using System.Text;

namespace CBS.Utils
{
    public static class CalendarTXTHandler
    {
        public const string NotAvailableText = "Not Available";
        public const string PositionPrefix = "Day ";


        public static string GetNotification(this CalendarInstance instance)
        {
            if (!instance.IsAvailable)
            {
                return NotAvailableText;
            }
            else
            {
                var endDate = instance.EndDate;
                var resetSpan = endDate.Subtract(DateTime.UtcNow.ToLocalTime());
                var totalDays = (int)resetSpan.TotalDays;
                var timeString = resetSpan.ToString(DateUtils.StoreTimerFormat);
                var sBuilder = new StringBuilder();
                sBuilder.Append("End in ");
                sBuilder.Append(totalDays > 0 ? totalDays + " Days " : string.Empty);
                sBuilder.Append(timeString);
                return sBuilder.ToString();
            }
        }

        public static string GetPositionText(this CalendarPosition position)
        {
            return PositionPrefix + (position.Position + 1).ToString();
        }
    }
}
