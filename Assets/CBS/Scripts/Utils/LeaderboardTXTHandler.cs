using System;
using System.Text;

namespace CBS.Utils
{
    public class LeaderboardTXTHandler
    {
        public static string GetVersionText(int version)
        {
            return "Version : " + version.ToString();
        }

        public static string GetNextResetNotification(DateTime resetDate)
        {
            var resetSpan = resetDate.Subtract(DateTime.UtcNow.ToLocalTime());
            if (resetSpan.Ticks > 0)
            {
                var totalDays = (int)resetSpan.TotalDays;
                var timeString = resetSpan.ToString(DateUtils.LeaderboardTimerFormat);
                var sBuilder = new StringBuilder();
                sBuilder.Append("End in ");
                sBuilder.Append(totalDays > 0 ? totalDays + " Days " : string.Empty);
                sBuilder.Append(timeString);
                return sBuilder.ToString();
            }
            else
            {
                return "00:00:00";
            }
        }
    }
}