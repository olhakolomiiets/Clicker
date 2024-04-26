using System;
using System.Text;

namespace CBS.Utils
{
    public static class BattlePassUtils
    {
        public static string GetFrameTimeLabel(DateTime endDate)
        {
            var resetSpan = endDate.Subtract(DateTime.UtcNow);
            var totalDays = (int)resetSpan.TotalDays;
            var timeString = resetSpan.ToString(DateUtils.StoreTimerFormat);
            var sBuilder = new StringBuilder();
            sBuilder.Append("End in ");
            sBuilder.Append(totalDays > 0 ? totalDays + " Days " : string.Empty);
            sBuilder.Append(timeString);
            return sBuilder.ToString();
        }

        public static string GetRewardLimitLabel(DateTime endDate)
        {
            var resetSpan = endDate.Subtract(DateTime.Now);
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
