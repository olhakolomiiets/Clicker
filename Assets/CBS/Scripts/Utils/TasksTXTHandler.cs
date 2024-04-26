using CBS.Models;
using System;
using System.Text;

namespace CBS.Utils
{
    public static class TasksTXTHandler
    {
        public const string NotCompleteText = "Task not yet completed";
        public const string CompleteText = "Task completed!";

        public static string GetLockText(int level, IntFilter filter)
        {
            if (filter == IntFilter.EQUAL_OR_GREATER)
            {
                return string.Format("Task is available at the level {0}", level);
            }
            else if (filter == IntFilter.EQUAL_OR_LESS)
            {
                return string.Format("Task available for level {0} or below", level);
            }
            else if (filter == IntFilter.EQUAL)
            {
                return string.Format("Task only available at level {0}", level);
            }
            return string.Empty;
        }

        public static string GetNextResetDateNotification(DateTime resetDate)
        {
            var resetSpan = resetDate.Subtract(DateTime.UtcNow.ToLocalTime());
            var totalDays = (int)resetSpan.TotalDays;
            var timeString = resetSpan.ToString(DateUtils.StoreTimerFormat);
            var sBuilder = new StringBuilder();
            sBuilder.Append("End in ");
            sBuilder.Append(totalDays > 0 ? totalDays + " Days " : string.Empty);
            sBuilder.Append(timeString);
            return sBuilder.ToString();
        }

        public static string GetNextResetUTCDateNotification(DateTime resetDate)
        {
            var resetSpan = resetDate.Subtract(DateTime.UtcNow);
            var totalDays = (int)resetSpan.TotalDays;
            var timeString = resetSpan.ToString(DateUtils.StoreTimerFormat);
            var sBuilder = new StringBuilder();
            sBuilder.Append("End in ");
            sBuilder.Append(totalDays > 0 ? totalDays + " Days " : string.Empty);
            sBuilder.Append(timeString);
            return sBuilder.ToString();
        }
    }
}
