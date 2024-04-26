using Azure.Data.Tables;
using CBS.Models;
using System;
using System.Threading.Tasks;

namespace CBS
{
    public class TimePeriodAssistant
    {
        private static readonly string LastCheckInKey = "LastCheckIn";
        private static readonly string FirstCheckInKey = "FirstCheckIn";
        private static readonly string RawDataKey = "RawData";
        private static readonly string RowKey = "RowKey";
        private static readonly string PartitionKey = "PartitionKey";

        private static readonly DateTime CalculateFromDate = new DateTime(2000,05,09,0,0,0);

        public static async Task<ExecuteResult<PeriodState>> GetPeriodStateAsync(string periodTableID, string entityID, string periodID, DatePeriod period, DateTime requestDate)
        {
            var entityResult = await CosmosTable.GetEntityAsync(periodTableID, entityID, periodID, GetPeriodEntityKeys());
            if (entityResult.Error != null)
            {
                return new ExecuteResult<PeriodState>
                {
                    Result = PeriodState.GetDefault(periodID, period)
                };
            }
            var periodEntity = entityResult.Result;
            var periodState = GetStateFromEntity(periodEntity, period, requestDate);

            return new ExecuteResult<PeriodState>
            {
                Result = periodState
            };
        }

        public static async Task<ExecuteResult<FunctionCheckInResult>> CheckIn(string periodTableID, string entityID, string periodID, DateTime requestDate, DatePeriod period, string rawData = null)
        {
            // check first check in date
            var entityResult = await CosmosTable.GetEntityAsync(periodTableID, entityID, periodID, new string [] { FirstCheckInKey });
            var insertFirstCheckIn = false;
            if (entityResult.Error != null)
            {
                insertFirstCheckIn = true;
            }
            else
            {
                var entity = entityResult.Result;
                var firstCheckIn = entity.GetInt32(FirstCheckInKey);
                insertFirstCheckIn = firstCheckIn == null;
            }
            var checkInIndex = GetPeriodIndex(requestDate, period);
            // generate new entity
            var periodEntity = new TableEntity();
            periodEntity.PartitionKey = entityID;
            periodEntity.RowKey = periodID;
            if (insertFirstCheckIn)
            {
                periodEntity[FirstCheckInKey] = checkInIndex;
            }
            periodEntity[LastCheckInKey] = checkInIndex;
            if (rawData != null)
            {
                periodEntity[RawDataKey] = rawData;
            }

            var upsertResult = await CosmosTable.UpsertEntityAsync(periodTableID, periodEntity);
            if (upsertResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCheckInResult>(upsertResult.Error);
            }

            return new ExecuteResult<FunctionCheckInResult>
            {
                Result = new FunctionCheckInResult
                {
                    SecondsToNextCheckIn = SecondsToNextCheckIn(requestDate, period),
                    DateOfNextCheckIn = DateOfNextCheckIn(requestDate, period)
                }
            };
        }

        public static async Task<ExecuteResult<string>> RemovePeriodInstanceAsync(string periodTableID, string entityID, string periodID)
        {
            var removeResult = await CosmosTable.DeleteEntityAsync(periodTableID, periodID, entityID);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<string>(removeResult.Error);
            }
            return new ExecuteResult<string>
            {
                Result = periodID
            };
        }

        public static async Task<ExecuteResult<string>> GetPeriodRawDataAsync(string periodTableID, string entityID, string periodID)
        {
            var entityResult = await CosmosTable.GetEntityAsync(periodTableID, entityID, periodID, new string [] { RawDataKey });
            if (entityResult.Error != null)
            {
                return new ExecuteResult<string>
                {
                    Result = null
                };
            }
            var entity = entityResult.Result;
            var rawData = entity.GetString(RawDataKey);
            return new ExecuteResult<string>
            {
                Result = rawData
            };
        }

        private static int GetPeriodIndex(DateTime requestDate, DatePeriod period)
        {
            var calculateFromDate = CalculateFromDate;
            if (period == DatePeriod.Week)
            {
                calculateFromDate = calculateFromDate.AddDays(-1);
            }
            var duration = requestDate.Subtract(calculateFromDate);
            var rawID = string.Empty;
            if (period == DatePeriod.Day)
            {
                return (int)Math.Ceiling(duration.TotalDays);
            }
            else if (period == DatePeriod.Week)
            {
                return (int)Math.Ceiling(duration.TotalDays/7);
            }
            else if (period == DatePeriod.Month)
            {
                return Math.Abs((requestDate.Month - CalculateFromDate.Month) + 12 * (requestDate.Year - CalculateFromDate.Year));
            }
            else if (period == DatePeriod.Year)
            {
                return requestDate.Year;
            }
            else if (period == DatePeriod.AllTime)
            {
                return 0;
            }
            return 0;
        }

        private static int SecondsToNextCheckIn(DateTime requestDate, DatePeriod period)
        {
            if (period == DatePeriod.Day)
            {
                var nextDate = requestDate.AddDays(1).Date;
                var span = (nextDate - requestDate);
                return (int)span.TotalSeconds;
            }
            else if (period == DatePeriod.Week)
            {
                int daysToAdd = ((int)DayOfWeek.Monday - (int)requestDate.DayOfWeek + 7) % 7;
                var nextMonday = requestDate.AddDays(daysToAdd).Date;
                var span = (nextMonday - requestDate);
                return (int)span.TotalSeconds;
            }
            else if (period == DatePeriod.Month)
            {
                var daysInMonth = DateTime.DaysInMonth(requestDate.Year, requestDate.Month);
                var nextMonth = requestDate.AddDays(daysInMonth - requestDate.Day + 1).Date;
                var span = (nextMonth - requestDate);
                return (int)span.TotalSeconds;
            }
            else if (period == DatePeriod.Year)
            {
                var daysLeft = new DateTime(DateTime.Now.Year, 12, 31).DayOfYear - requestDate.DayOfYear;
                var nextYear = requestDate.AddDays(daysLeft + 1).Date;
                var span = (nextYear - requestDate);
                return (int)span.TotalSeconds;
            }
            return 0;
        }

        public static DateTime? DateOfNextCheckIn(DateTime requestDate, DatePeriod period)
        {
            if (period == DatePeriod.Day)
            {
                return requestDate.AddDays(1).Date;
            }
            else if (period == DatePeriod.Week)
            {
                int daysToAdd = LeftDaysToNextWeek(requestDate.DayOfWeek);
                return requestDate.AddDays(daysToAdd).Date;
            }
            else if (period == DatePeriod.Month)
            {
                var daysInMonth = DateTime.DaysInMonth(requestDate.Year, requestDate.Month);
                return requestDate.AddDays(daysInMonth - requestDate.Day + 1).Date;
            }
            else if (period == DatePeriod.Year)
            {
                var daysLeft = new DateTime(DateTime.Now.Year, 12, 31).DayOfYear - requestDate.DayOfYear;
                return requestDate.AddDays(daysLeft + 1).Date;
            }
            return null;
        }

        // internal
        private static string [] GetPeriodEntityKeys()
        {
            return new string [] 
            {
                RowKey,
                PartitionKey,
                LastCheckInKey,
                FirstCheckInKey,
                RawDataKey
            };
        }

        private static PeriodState GetStateFromEntity(TableEntity entity, DatePeriod period, DateTime requestDate)
        {
            var ownerID = entity.PartitionKey;
            var periodID = entity.RowKey;
            var lastCheckIn = entity.GetInt32(LastCheckInKey);
            var firstCheckIn = entity.GetInt32(FirstCheckInKey);
            var rawData = entity.GetString(RawDataKey);

            var checkInIndex = GetPeriodIndex(requestDate, period);
            var secondsToNext = SecondsToNextCheckIn(requestDate, period);
            var checkInDate = DateOfNextCheckIn(requestDate, period);
            var isCheckinedAvailable = lastCheckIn == null || checkInIndex > (int)lastCheckIn;
            var hasAnyCheckin = firstCheckIn != null;
            var totalPassed = 0;
            if (hasAnyCheckin)
            {
                totalPassed = checkInIndex - (int)firstCheckIn;
            }

            return new PeriodState
            {
                PeriodID = periodID,
                Period = period,
                CheckinAvailable = isCheckinedAvailable,
                CheckinIndex = checkInIndex,
                TotalPassedPeriod = totalPassed,
                SecondsToNextCheckin = secondsToNext,
                NextCheckIn = checkInDate,
                RawData = rawData
            };
        }

        private static int LeftDaysToNextWeek(DayOfWeek dayOfWeek)
        {
            switch(dayOfWeek)
            {
                case DayOfWeek.Monday:
                {
                    return 7;
                }
                case DayOfWeek.Tuesday:
                {
                    return 6;
                }
                case DayOfWeek.Wednesday:
                {
                    return 5;
                }
                case DayOfWeek.Thursday:
                {
                    return 4;
                }
                case DayOfWeek.Friday:
                {
                    return 3;
                }
                case DayOfWeek.Saturday:
                {
                    return 2;
                }
                case DayOfWeek.Sunday:
                {
                    return 1;
                }
            }
            return 0;
        }
    }
}