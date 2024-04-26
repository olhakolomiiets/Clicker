using CBS.Models;
using System;
using System.Text;

namespace CBS.Utils
{
    public class StoreTXTHandler
    {
        public static string GetLimitationNotification(DatePeriod period, int left)
        {
            var sBuilder = new StringBuilder();
            if (period == DatePeriod.Day)
            {
                sBuilder.Append("Available today : ");
            }
            else if (period == DatePeriod.Week)
            {
                sBuilder.Append("Available this week : ");
            }
            else if (period == DatePeriod.Month)
            {
                sBuilder.Append("Available this month : ");
            }
            else if (period == DatePeriod.Year)
            {
                sBuilder.Append("Available this year : ");
            }
            else if (period == DatePeriod.AllTime)
            {
                sBuilder.Append("Available all the time : ");
            }
            sBuilder.Append(left);

            return sBuilder.ToString();
        }

        public static string GetNextResetLimitNotification(DateTime resetDate, DatePeriod period)
        {
            if (period == DatePeriod.AllTime)
            {
                return "Not available";
            }
            else
            {
                var resetSpan = resetDate.Subtract(DateTime.UtcNow.ToLocalTime());
                var totalDays = (int)resetSpan.TotalDays;
                var timeString = resetSpan.ToString(DateUtils.StoreTimerFormat);
                var sBuilder = new StringBuilder();
                sBuilder.Append("Available in ");
                sBuilder.Append(totalDays > 0 ? totalDays + " Days " : string.Empty);
                sBuilder.Append(timeString);
                return sBuilder.ToString();
            }
        }
    }
}
