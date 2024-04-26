using System;
using System.Text;

namespace CBS.Utils
{
    public static class ProfileTXTHandler
    {
        public const string ONLINE_TITLE = "Online";

        public static string GetAvatarLockText(int lockLevel)
        {
            return "avatar available at level " + lockLevel.ToString();
        }

        public static string GetLastOnlineNotification(TimeSpan span)
        {
            var totalDays = (int)span.TotalDays;
            var totalHours = (int)span.TotalHours;
            var totalMinutes = (int)span.TotalMinutes;
            var sBuilder = new StringBuilder();
            sBuilder.Append("Last seen ");
            if (totalDays > 0)
            {
                sBuilder.Append(totalDays + "d");
            }
            else if (totalHours > 0)
            {
                sBuilder.Append(totalHours + "h");
            }
            else if (totalMinutes > 0)
            {
                sBuilder.Append(totalMinutes + "m");
            }
            else
            {
                return string.Empty;
            }
            sBuilder.Append(" ago");
            return sBuilder.ToString();
        }
    }
}
