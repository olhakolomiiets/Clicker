using System;

namespace CBS.Utils
{
    public class DateUtils
    {
        public const string DefaultTimer = "00:00";
        public const string TournamentTimerFormat = @"hh\:mm\:ss";
        public const string StoreTimerFormat = @"hh\:mm\:ss";
        public const string LeaderboardTimerFormat = @"hh\:mm\:ss";
        public const string ProfileDateFormat = @"hh\:mm";
        public const string CurrencyTimerFormat = @"mm\:ss";
        public const string EventTimerFormat = @"dd\D\:hh\:mm\:ss";
        public const string LootboxTimerFormat = @"dd\:hh\:mm\:ss";
#if UNITY_EDITOR
        private static int TestHoursOffset = 0;
#endif

        public static DateTime ConvertFromUnixTimestamp(long timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return origin.AddMilliseconds(timestamp);
        }

        public static DateTime GetDateFromString(string data)
        {
            return DateTime.Parse(data).ToLocalTime();
        }

        public static int TimestampToHours(int timestamp)
        {
            var timeSpan = TimeSpan.FromMilliseconds(timestamp);
            return (int)timeSpan.TotalHours;
        }

        public static int TournamentDateToHours(TournamentDate date)
        {
            if (date == TournamentDate.Daily)
            {
                var timeSpan = TimeSpan.FromDays(1);
                return (int)timeSpan.TotalHours;
            }
            else if (date == TournamentDate.Hourly)
            {
                var timeSpan = TimeSpan.FromHours(1);
                return (int)timeSpan.TotalHours;
            }
            else if (date == TournamentDate.Weekly)
            {
                var timeSpan = TimeSpan.FromDays(7);
                return (int)timeSpan.TotalHours;
            }
            else if (date == TournamentDate.Monthly)
            {
                var timeSpan = TimeSpan.FromDays(30);
                return (int)timeSpan.TotalHours;
            }
            else if (date == TournamentDate.Yearly)
            {
                var timeSpan = TimeSpan.FromDays(365);
                return (int)timeSpan.TotalHours;
            }
            else
            {
                return 0;
            }
        }

        public static int HoursToMiliseconds(int hours)
        {
            return hours * 3600000;
        }

        public static int ToutnamentDateMiliseconds(TournamentDate date)
        {
            if (date == TournamentDate.Daily)
            {
                var timeSpan = TimeSpan.FromDays(1);
                return (int)timeSpan.TotalMilliseconds;
            }
            else if (date == TournamentDate.Hourly)
            {
                var timeSpan = TimeSpan.FromHours(1);
                return (int)timeSpan.TotalMilliseconds;
            }
            else if (date == TournamentDate.Weekly)
            {
                var timeSpan = TimeSpan.FromDays(7);
                return (int)timeSpan.TotalMilliseconds;
            }
            else if (date == TournamentDate.Monthly)
            {
                var timeSpan = TimeSpan.FromDays(30);
                return (int)timeSpan.TotalMilliseconds;
            }
            else if (date == TournamentDate.Yearly)
            {
                var timeSpan = TimeSpan.FromDays(365);
                return (int)timeSpan.TotalMilliseconds;
            }
            else
            {
                return 0;
            }
        }

        public static int GetZoneOffset()
        {
#if UNITY_2020_2_OR_NEWER
            var curTimeZone = TimeZoneInfo.Local;
#else
            var curTimeZone = TimeZone.CurrentTimeZone;
#endif
            var offset = curTimeZone.GetUtcOffset(DateTime.Now);
#if UNITY_EDITOR
            return (int)offset.TotalMilliseconds + HoursToMiliseconds(TestHoursOffset);
#else
            return (int)offset.TotalMilliseconds;
#endif
        }
#if UNITY_EDITOR
        public static void AddTestHoursToCuurentSession(int hours)
        {
            TestHoursOffset += hours;
        }
#endif
    }
}
