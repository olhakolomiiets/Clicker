

using System;

namespace CBS.Models
{
    public class PeriodState
    {
        public string PeriodID;
        public DatePeriod Period;
        public bool CheckinAvailable;
        public int TotalPassedPeriod;
        public int CheckinIndex;
        public int SecondsToNextCheckin;
        public bool HasAnyCheckin;
        public DateTime? NextCheckIn;
        public string RawData;

        public static PeriodState GetDefault(string periodID, DatePeriod period)
        {
            return new PeriodState
            {
                PeriodID = periodID,
                Period = period,
                CheckinAvailable = true,
                TotalPassedPeriod = 0,
                CheckinIndex = 0,
                SecondsToNextCheckin = 0,
                RawData = null
            };
        }
    }
}
