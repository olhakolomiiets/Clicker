using PlayFab.ServerModels;
using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using System.Linq;
using System.Collections.Generic;
using System;

namespace CBS
{
    public class CalendarModule : BaseAzureModule
    {
        private static readonly string CalendarPeriodTableID = "CBSProfileCalendar";

        [FunctionName(AzureFunctions.GetAllCalendarsMethod)]
        public static async Task<dynamic> GetAllCalendarsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionCalendarRequest>();
            var profileID = request.ProfileID;
            var timeZone = request.TimeZone;

            var getResult = await GetAllCalendarStatesForProfileAsync(profileID, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetCalendarMethod)]
        public static async Task<dynamic> GetCalendarTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionCalendarRequest>();
            var profileID = request.ProfileID;
            var calendarID = request.CalendarID;
            var timeZone = request.TimeZone;

            var getResult = await GetCalendarStateForProfileAsync(profileID, calendarID, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PickupCalendarRewardMethod)]
        public static async Task<dynamic> PickupCalendarRewardTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionCalendarRewardRequest>();
            var profileID = request.ProfileID;
            var calendarID = request.CalendarID;
            var timeZone = request.TimeZone;
            var rewardIndex = request.Index;

            var pickupResult = await PickupCalendarRewardAsync(profileID, calendarID, rewardIndex, timeZone);
            if (pickupResult.Error != null)
            {
                return ErrorHandler.ThrowError(pickupResult.Error).AsFunctionResult();
            }

            return pickupResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ResetCalendarMethod)]
        public static async Task<dynamic> ResetCalendarTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionCalendarRequest>();
            var profileID = request.ProfileID;
            var calendarID = request.CalendarID;
            var timeZone = request.TimeZone;

            var getResult = await GetCalendarStateForProfileAsync(profileID, calendarID, timeZone, true);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetCalendarBadgeMethod)]
        public static async Task<dynamic> GetCalendarBadgeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionCalendarRequest>();
            var profileID = request.ProfileID;
            var timeZone = request.TimeZone;

            var getResult = await GetCalendarBadgeAsync(profileID, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GrantCalendarMethod)]
        public static async Task<dynamic> GrantCalendarTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionCalendarRequest>();
            var profileID = request.ProfileID;
            var calendarID = request.CalendarID;
            var timeZone = request.TimeZone;

            var grantResult = await GrantCalendarAndGetInstanceAsync(profileID, calendarID, timeZone);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError(grantResult.Error).AsFunctionResult();
            }

            return grantResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PreCalendarPurchaseProccessMethod)]
        public static async Task<dynamic> PreCalendarPurchaseProccessTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionCalendarRequest>();
            var profileID = request.ProfileID;
            var calendarID = request.CalendarID;

            var validateResult = await PrePurchaseValidationAsync(profileID, calendarID);
            if (validateResult.Error != null)
            {
                return ErrorHandler.ThrowError(validateResult.Error).AsFunctionResult();
            }

            return validateResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionCalendarDependencyResult>> GetCalendarDependencyAsync(string profileID)
        {
            var getContainerResult =  await GetInternalTitleDataAsObjectAsync<CalendarContainer>(TitleKeys.CalendarTitleKey);
            if (getContainerResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCalendarDependencyResult>(getContainerResult.Error);
            }
            var container = getContainerResult.Result ?? new CalendarContainer();

            if (container.IsEmpty())
            {
                return ErrorHandler.CalendarNotConfigured<FunctionCalendarDependencyResult>();
            }

            var inventoryRequest = new GetUserInventoryRequest
            {
                PlayFabId = profileID
            };
            var getInventoryResult = await FabServerAPI.GetUserInventoryAsync(inventoryRequest);
            if (getInventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCalendarDependencyResult>(getInventoryResult.Error);
            }
            var inventory = getInventoryResult.Result.Inventory ?? new List<ItemInstance>();
            var calendarInventory = inventory.Where(x=>x.CatalogVersion == CatalogKeys.CalendarCatalogID);

            return new ExecuteResult<FunctionCalendarDependencyResult>
            {
                Result = new FunctionCalendarDependencyResult
                {
                    Container = container,
                    Inventory = calendarInventory.ToList()
                }
            };
        }

        public static async Task<ExecuteResult<FunctionAllCalendarsResult>> GetAllCalendarStatesForProfileAsync(string profileID, int timeZone)
        {
            var getDependencyResult = await GetCalendarDependencyAsync(profileID);
            if (getDependencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAllCalendarsResult>(getDependencyResult.Error);
            }
            var dependency = getDependencyResult.Result;
            var container = dependency.Container;
            var calendarInstances = container.Instances ?? new List<CalendarInstance>();
            var calendarIDs = calendarInstances.Select(x=>x.ID);
            var taskList = new List<Task<ExecuteResult<CalendarInstance>>>();
            foreach (var calendarID in calendarIDs)
            {
                taskList.Add(GetCalendarStateForProfileAsync(profileID, calendarID, timeZone));
            }   
            var statesResult = await Task.WhenAll(taskList);
            var availableCalendars = statesResult.Where(x=>x.Error == null && x.Result != null).Select(x=>x.Result).ToList();

            return new ExecuteResult<FunctionAllCalendarsResult>
            {
                Result = new FunctionAllCalendarsResult
                {
                    Instances = availableCalendars
                }
            };
        }

        public static async Task<ExecuteResult<CalendarInstance>> GetCalendarStateForProfileAsync(string profileID, string calendarID, int timeZone, bool forceReset = false)
        {
            var getDependencyResult = await GetCalendarDependencyAsync(profileID);
            if (getDependencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<CalendarInstance>(getDependencyResult.Error);
            }
            var dependency = getDependencyResult.Result;

            var parseResult = await ParseCalendarStateAsync(profileID, dependency, calendarID, timeZone, forceReset);
            if (parseResult.Error != null)
            {
                return ErrorHandler.ThrowError<CalendarInstance>(parseResult.Error);
            }
            var calendarInstance = parseResult.Result;

            return new ExecuteResult<CalendarInstance>
            {
                Result = calendarInstance
            };
        }

        public static async Task<ExecuteResult<CalendarInstance>> ParseCalendarStateAsync(string profileID, FunctionCalendarDependencyResult dependency, string calendarID, int timeZone, bool forceReset = false)
        {
            var container = dependency.Container;
            var inventory = dependency.Inventory;

            var calendarInstance = container.GetInstanceByID(calendarID);
            if (calendarInstance == null)
            {
                return ErrorHandler.CalendarNotConfigured<CalendarInstance>();
            }

            var template = calendarInstance.Template;
            var positions = calendarInstance.Positions ?? new List<CalendarPosition>();
            var requestDate = ServerTimeUTC.AddMilliseconds(timeZone);
            var calendarAvalilable = calendarInstance.Enabled;
            var looped = calendarInstance.Looped;
            var noPenalty = calendarInstance.NoPenalty;
            var activation = calendarInstance.Activation;
            var currentIndex = -1;
            var maxIndex = 0;
            var available = false;
            var badgeCount = 0;
            var endDate = ServerTimeUTC;
            var purchased = inventory.Any(x=>x.ItemId == calendarID) && activation == ActivationType.BY_PURCHASE;

            if (calendarAvalilable == false && !purchased)
            {
                return ErrorHandler.CalendarNotEnabled<CalendarInstance>();
            }

            if (template == CalendarTemplate.MONTHLY_TEMPLATE)
            {
                var monthIndex = requestDate.Month - 1;
                currentIndex = requestDate.Day - 1;
                positions = calendarInstance.GetMonthlyPositions(monthIndex);
                maxIndex = positions.Count - 1;
                calendarInstance.InstanceID = calendarInstance.GetTemplateInstanceID();
                available = true; 
                endDate = TimePeriodAssistant.DateOfNextCheckIn(requestDate, DatePeriod.Month).GetValueOrDefault(); 

                // check if calendar ended
                var periodID = calendarInstance.InstanceID;
                var periodStateResult = await TimePeriodAssistant.GetPeriodStateAsync(CalendarPeriodTableID, profileID, periodID, DatePeriod.Month, requestDate);
                if (periodStateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<CalendarInstance>(periodStateResult.Error);
                }
                var periodState = periodStateResult.Result;
                var periodExpired = periodState.CheckinAvailable;
                if (periodExpired || forceReset)
                {
                    var resetMetaResult = await ResetProfileCalendarMetaAsync(profileID, calendarInstance.InstanceID);
                    if (resetMetaResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<CalendarInstance>(resetMetaResult.Error);
                    }

                    var checkResult = await TimePeriodAssistant.CheckIn(CalendarPeriodTableID, profileID, periodID, requestDate, DatePeriod.Month);
                    if (checkResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<CalendarInstance>(checkResult.Error);
                    }
                }
            }
            else if (template == CalendarTemplate.WEEKLY_TEMPLATE)
            {
                currentIndex = requestDate.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)requestDate.DayOfWeek - 1;
                positions = calendarInstance.GetWeeklyPositions();
                maxIndex = positions.Count - 1;
                calendarInstance.InstanceID = calendarInstance.GetTemplateInstanceID();
                available = true; 
                endDate = TimePeriodAssistant.DateOfNextCheckIn(requestDate, DatePeriod.Week).GetValueOrDefault(); 
                // check if calendar ended
                var periodID = calendarInstance.InstanceID;
                var periodStateResult = await TimePeriodAssistant.GetPeriodStateAsync(CalendarPeriodTableID, profileID, periodID, DatePeriod.Week, requestDate);
                if (periodStateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<CalendarInstance>(periodStateResult.Error);
                }
                var periodState = periodStateResult.Result;
                var periodExpired = periodState.CheckinAvailable;
                if (periodExpired || forceReset)
                {
                    var resetMetaResult = await ResetProfileCalendarMetaAsync(profileID, calendarInstance.InstanceID);
                    if (resetMetaResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<CalendarInstance>(resetMetaResult.Error);
                    }

                    var checkResult = await TimePeriodAssistant.CheckIn(CalendarPeriodTableID, profileID, periodID, requestDate, DatePeriod.Week);
                    if (checkResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<CalendarInstance>(checkResult.Error);
                    }
                }
            }
            else
            {
                var dontIncreaseIndexWhileMissCheckIn = calendarInstance.DontIncrementIndexWhenSkipping;
                // get instance id
                if (activation == ActivationType.ALWAYS_AVAILABLE)
                {
                    calendarInstance.InstanceID = calendarInstance.GetTemplateInstanceID();
                }
                else
                {
                    var itemInstance = inventory.FirstOrDefault(x=>x.ItemId == calendarID);
                    if (itemInstance == null)
                    {
                        calendarInstance.InstanceID = calendarID;
                    }
                    else
                    {
                        calendarInstance.InstanceID = itemInstance.ItemInstanceId;
                    }
                }
                if (forceReset)
                {
                    var periodID = calendarInstance.InstanceID;
                    var resetMetaResult = await ResetProfileCalendarMetaAsync(profileID, calendarInstance.InstanceID);
                    if (resetMetaResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<CalendarInstance>(resetMetaResult.Error);
                    }
                }
                // set availability               
                available = activation == ActivationType.ALWAYS_AVAILABLE || purchased;
                maxIndex = positions.Count - 1;
                var totalPassed = 0;
                var secondsToNextCheckIn = 0;
                // check daily activity
                if (available)
                {
                    var periodID = calendarInstance.InstanceID;
                    var periodStateResult = await TimePeriodAssistant.GetPeriodStateAsync(CalendarPeriodTableID, profileID, periodID, DatePeriod.Day, requestDate);
                    if (periodStateResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<CalendarInstance>(periodStateResult.Error);
                    }
                    var periodState = periodStateResult.Result;
                    secondsToNextCheckIn = periodState.SecondsToNextCheckin;
                    totalPassed = periodState.TotalPassedPeriod;

                    var profileMetaResult = await GetProfileCalendarMetaAsync(profileID, calendarInstance.InstanceID);
                    if (profileMetaResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<CalendarInstance>(profileMetaResult.Error);
                    }
                    var profileCalendarMeta = profileMetaResult.Result;
                    var rewardedItems = profileCalendarMeta.RewardedItems ?? new Dictionary<int, bool>(); 
                    var totalClaimedDays = rewardedItems.Values.Where(x=>x == true).Count();
                    
                    if (dontIncreaseIndexWhileMissCheckIn)
                    {
                        var rawData = periodStateResult.Result.RawData;
                        if (!int.TryParse(rawData, out currentIndex))
                        {
                            currentIndex = 0;
                        }

                        var thisDayClaimed = profileCalendarMeta.IsRewarded(currentIndex);
                        currentIndex = thisDayClaimed && !periodState.CheckinAvailable ? totalClaimedDays - 1 : totalClaimedDays; 
                    }
                    else
                    {
                        currentIndex = totalPassed;
                    }
                    
                    if (periodState.CheckinAvailable && dontIncreaseIndexWhileMissCheckIn)
                    {                     
                        currentIndex++;
                        var checkinResult = await TimePeriodAssistant.CheckIn(CalendarPeriodTableID, profileID, periodID, requestDate, DatePeriod.Day, currentIndex.ToString());
                        if (checkinResult.Error != null)
                        {
                            return ErrorHandler.ThrowError<CalendarInstance>(checkinResult.Error);
                        }
                    }
                    else if (!periodState.HasAnyCheckin)
                    {
                        var checkinResult = await TimePeriodAssistant.CheckIn(CalendarPeriodTableID, profileID, periodID, requestDate, DatePeriod.Day);
                        if (checkinResult.Error != null)
                        {
                            return ErrorHandler.ThrowError<CalendarInstance>(checkinResult.Error);
                        }
                    }              
                } 
                else
                {
                    totalPassed = currentIndex;
                }
                // check if calendar ended
                var calendarEnded = totalPassed > maxIndex || currentIndex > maxIndex;
                if (calendarEnded)
                {
                    var periodID = calendarInstance.InstanceID;
                    currentIndex = -1;
                    
                    if (looped)
                    {
                        // remove rewarded items info
                        var resetMetaResult = await ResetProfileCalendarMetaAsync(profileID, calendarInstance.InstanceID);
                        if (resetMetaResult.Error != null)
                        {
                            return ErrorHandler.ThrowError<CalendarInstance>(resetMetaResult.Error);
                        }
                        currentIndex = 0;
                        var checkinResult = await TimePeriodAssistant.CheckIn(CalendarPeriodTableID, profileID, periodID, requestDate, DatePeriod.Day, currentIndex.ToString());
                        if (checkinResult.Error != null)
                        {
                            return ErrorHandler.ThrowError<CalendarInstance>(checkinResult.Error);
                        }
                    }
                    else
                    {
                        available = false;                      
                        if (purchased)
                        {
                            // remove rewarded items info
                            var resetMetaResult = await ResetProfileCalendarMetaAsync(profileID, calendarInstance.InstanceID);
                            if (resetMetaResult.Error != null)
                            {
                                return ErrorHandler.ThrowError<CalendarInstance>(resetMetaResult.Error);
                            }
                            // remove purchased item from inventory
                            var revokeList = new List<RevokeInventoryItem>();
                            revokeList.Add(new RevokeInventoryItem
                            {
                                PlayFabId = profileID,
                                ItemInstanceId = calendarInstance.InstanceID
                            });
                            var revokeRequest = new RevokeInventoryItemsRequest
                            {
                                Items = revokeList
                            };
                            var revokeResult = await FabServerAPI.RevokeInventoryItemsAsync(revokeRequest);
                            if (revokeResult.Error != null)
                            {
                                ErrorHandler.ThrowError<CalendarInstance>(revokeResult.Error);
                            }
                        }
                        else
                        {
                            return ErrorHandler.CalendarNotEnabled<CalendarInstance>();
                        }
                    }
                }
                else
                {
                    endDate = TimePeriodAssistant.DateOfNextCheckIn(requestDate, DatePeriod.Day).GetValueOrDefault(); 
                    var notPassedIndex = maxIndex - currentIndex;
                    endDate = endDate.AddDays(notPassedIndex);
                }
            }

            // get calendar meta
            var instanceID = calendarInstance.InstanceID;
            var metaResult = await GetProfileCalendarMetaAsync(profileID, instanceID);
            if (metaResult.Error != null)
            {
                return ErrorHandler.ThrowError<CalendarInstance>(metaResult.Error);
            }
            var calendarMeta = metaResult.Result;

            // parse positions
            var positionCount = positions.Count;
            for (int i=0;i<positionCount;i++)
            {
                var position = positions[i];
                position.Active = i == currentIndex;
                position.CanBeRewarded = i == currentIndex && !calendarMeta.IsRewarded(i);
                if (noPenalty)
                {
                    position.CanBeRewarded = i <= currentIndex && !calendarMeta.IsRewarded(i);
                }
                else
                {
                    position.Missed = i < currentIndex && !calendarMeta.IsRewarded(i);
                }
                position.Rewarded = calendarMeta.IsRewarded(i);
                if (position.CanBeRewarded)
                {
                    badgeCount++;
                }
            }
            calendarInstance.Positions = positions;
            calendarInstance.CurrentIndex = currentIndex;
            calendarInstance.IsAvailable = available;
            calendarInstance.EndDate = endDate;
            calendarInstance.BadgeCount = badgeCount;

            return new ExecuteResult<CalendarInstance>
            {
                Result = calendarInstance
            };
        }

        public static async Task<ExecuteResult<CalendarProfileMeta>> GetProfileCalendarMetaAsync(string profileID, string calendarInstanceID)
        {
            var profileMetaResult = await GetProfileInternalDataAsObject<CalendarProfileMeta>(profileID, calendarInstanceID);
            if (profileMetaResult.Error != null)
            {
                return ErrorHandler.ThrowError<CalendarProfileMeta>(profileMetaResult.Error);
            }
            var metaData = profileMetaResult.Result ?? new CalendarProfileMeta();

            return new ExecuteResult<CalendarProfileMeta>
            {
                Result = metaData
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SaveProfileCalendarMetaAsync(string profileID, string calendarInstanceID, CalendarProfileMeta meta)
        {
            var rawData = JsonConvert.SerializeObject(meta);
            var profileSaveResult = await SaveProfileInternalDataAsync(profileID, calendarInstanceID, rawData);
            if (profileSaveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(profileSaveResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ResetProfileCalendarMetaAsync(string profileID, string calendarInstanceID)
        {
            var profileResultResult = await SaveProfileCalendarMetaAsync(profileID, calendarInstanceID, new CalendarProfileMeta());
            if (profileResultResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(profileResultResult.Error);
            }
            await TimePeriodAssistant.RemovePeriodInstanceAsync(CalendarPeriodTableID, profileID, calendarInstanceID);

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionPickupCalendarRewardResult>> PickupCalendarRewardAsync(string profileID, string calendarID, int positionIndex, int timeZone)
        {
            var getCalendarResult = await GetCalendarStateForProfileAsync(profileID, calendarID, timeZone);
            if (getCalendarResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPickupCalendarRewardResult>(getCalendarResult.Error);
            }
            var calendarInstance = getCalendarResult.Result;
            var available = calendarInstance.IsAvailable;
            var enabled = calendarInstance.Enabled;
            var instanceID = calendarInstance.InstanceID;
            if (!available)
            {
                return ErrorHandler.CalendarNotEnabled<FunctionPickupCalendarRewardResult>();
            }

            var positions = calendarInstance.Positions ?? new List<CalendarPosition>();
            var validIndex = positions.Count > 0 && positionIndex >=0 && positionIndex < positions.Count;
            if (!validIndex)
            {
                return ErrorHandler.InvalidIndex<FunctionPickupCalendarRewardResult>();
            }
            var currentPosition = positions[positionIndex];
            var rewarded = currentPosition.Rewarded;
            var canBeRewarded = currentPosition.CanBeRewarded;
            if (rewarded)
            {
                return ErrorHandler.AlreadyRewarded<FunctionPickupCalendarRewardResult>();
            }
            if (!canBeRewarded)
            {
                return ErrorHandler.RewardNotAvailable<FunctionPickupCalendarRewardResult>();
            }
            var reward = currentPosition.Reward;

            var grantRewardResult = await RewardModule.GrantRewardToProfileAsync(reward, profileID);
            if (grantRewardResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPickupCalendarRewardResult>(grantRewardResult.Error);
            }
            var grantResult = grantRewardResult.Result;

            currentPosition.CanBeRewarded = false;
            currentPosition.Rewarded = true;

            var metaResult = await GetProfileCalendarMetaAsync(profileID, instanceID);
            if (metaResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPickupCalendarRewardResult>(metaResult.Error);
            }
            var calendarMeta = metaResult.Result;
            calendarMeta.AddReward(positionIndex);

            var saveMetaResult = await SaveProfileCalendarMetaAsync(profileID, instanceID, calendarMeta);
            if (saveMetaResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPickupCalendarRewardResult>(saveMetaResult.Error);
            }

            var profileEvents = currentPosition.Events;
            if (profileEvents != null)
            {
                EventModule.ExecuteProfileEventContainer(profileID, profileEvents);
            }

            return new ExecuteResult<FunctionPickupCalendarRewardResult>
            {
                Result = new FunctionPickupCalendarRewardResult
                {
                    RewardResult = grantResult,
                    UpdatedPosition = currentPosition
                }
            };
        }

        public static async Task<ExecuteResult<FunctionBadgeResult>> GetCalendarBadgeAsync(string profileID, int timeZone)
        {
            var allCalendarResult = await GetAllCalendarStatesForProfileAsync(profileID, timeZone);
            if (allCalendarResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBadgeResult>(allCalendarResult.Error);
            }
            var instances = allCalendarResult.Result.Instances ?? new List<CalendarInstance>();
            var badgeCount = instances.Sum(x=>x.BadgeCount);

            return new ExecuteResult<FunctionBadgeResult>
            {
                Result = new FunctionBadgeResult
                {
                    Count = badgeCount
                }
            };
        }

        public static async Task<ExecuteResult<CalendarInstance>> GrantCalendarAndGetInstanceAsync(string profileID, string calendarID, int timeZone)
        {
            var grantResult = await GrantCalendarInstanceToProfileAsync(profileID, calendarID);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError<CalendarInstance>(grantResult.Error);
            }
            var getInstanceResult = await GetCalendarStateForProfileAsync(profileID, calendarID, timeZone);
            if (getInstanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<CalendarInstance>(getInstanceResult.Error);
            }
            var instance = getInstanceResult.Result;

            return new ExecuteResult<CalendarInstance>
            {
                Result = instance
            };
        }

        public static async Task<ExecuteResult<FunctionGrantCalendarResult>> GrantCalendarInstanceToProfileAsync(string profileID, string calendarID)
        {
            var getDependencyResult = await GetCalendarDependencyAsync(profileID);
            if (getDependencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantCalendarResult>(getDependencyResult.Error);
            }
            var dependency = getDependencyResult.Result;
            var container = dependency.Container ?? new CalendarContainer();
            var inventory = dependency.Inventory ?? new List<ItemInstance>();

            var celendarInstance = container.GetInstanceByID(calendarID);
            var activation = celendarInstance.Activation;

            // check config
            if (celendarInstance == null)
            {
                return ErrorHandler.CalendarNotFound<FunctionGrantCalendarResult>();
            }
            if (activation != ActivationType.BY_PURCHASE)
            {
                return ErrorHandler.CalendarCanNotBePurchased<FunctionGrantCalendarResult>();
            }

            // check inventoty
            var calendarExist = inventory.Any(x=>x.ItemId == calendarID);
            if (calendarExist)
            {
                return ErrorHandler.CalendarAlreadyBePurchased<FunctionGrantCalendarResult>();
            }

            // grant calendar
            var grantResult = await InternalGrantItemsToPlayerAsync(CatalogKeys.CalendarCatalogID, new List<string>{calendarID}, profileID);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantCalendarResult>(grantResult.Error);
            }
            var grantList = grantResult.Result;
            var calendarResult = grantList.ItemGrantResults.FirstOrDefault();
            var instanceID = calendarResult.ItemInstanceId;

            return new ExecuteResult<FunctionGrantCalendarResult>
            {
                Result = new FunctionGrantCalendarResult
                {
                    CalendarID = calendarID,
                    InstanceID = instanceID
                }
            };
        }

        public static async Task<ExecuteResult<FunctionCalendarPurchaseValidationResult>> PrePurchaseValidationAsync(string profileID, string calendarID)
        {
            var getDependencyResult = await GetCalendarDependencyAsync(profileID);
            if (getDependencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCalendarPurchaseValidationResult>(getDependencyResult.Error);
            }
            var dependency = getDependencyResult.Result;
            var container = dependency.Container ?? new CalendarContainer();
            var inventory = dependency.Inventory ?? new List<ItemInstance>();

            var celendarInstance = container.GetInstanceByID(calendarID);
            var activation = celendarInstance.Activation;

            // check config
            if (celendarInstance == null)
            {
                return ErrorHandler.CalendarNotFound<FunctionCalendarPurchaseValidationResult>();
            }
            if (activation != ActivationType.BY_PURCHASE)
            {
                return ErrorHandler.CalendarCanNotBePurchased<FunctionCalendarPurchaseValidationResult>();
            }

            // check inventoty
            var calendarExist = inventory.Any(x=>x.ItemId == calendarID);
            if (calendarExist)
            {
                return ErrorHandler.CalendarAlreadyBePurchased<FunctionCalendarPurchaseValidationResult>();
            }

            var price = celendarInstance.Price;

            return new ExecuteResult<FunctionCalendarPurchaseValidationResult>
            {
                Result = new FunctionCalendarPurchaseValidationResult
                {
                    Price = price
                }
            };
        }

        public static async Task<ExecuteResult<FunctionCatalogItemsResult>> GetCalendarCatalogItemsAsync()
        {
            var request = new GetCatalogItemsRequest
            {
                CatalogVersion = CatalogKeys.CalendarCatalogID
            };
            var itemsResult = await FabServerAPI.GetCatalogItemsAsync(request);
            if (itemsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCatalogItemsResult>(itemsResult.Error);
            }
            var fabItems = itemsResult.Result.ToClientInstance().Catalog;

            return new ExecuteResult<FunctionCatalogItemsResult>{
                Result = new FunctionCatalogItemsResult
                {
                    Items = fabItems
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SetCalendarActivityAsync(string calendarID, bool activity)
        {
            var getContainerResult =  await GetInternalTitleDataAsObjectAsync<CalendarContainer>(TitleKeys.CalendarTitleKey);
            if (getContainerResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCalendarDependencyResult>(getContainerResult.Error);
            }
            var container = getContainerResult.Result ?? new CalendarContainer();

            if (container.IsEmpty())
            {
                return ErrorHandler.CalendarNotConfigured<FunctionEmptyResult>();
            }

            var calendar = container.GetInstanceByID(calendarID);
            if (calendar == null)
            {
                return ErrorHandler.CalendarNotFound<FunctionEmptyResult>();
            }

            calendar.Enabled = activity;

            var containerRawData = JsonPlugin.ToJsonCompress(container);

            var saveResult = await SaveInternalTitleDataAsync(TitleKeys.CalendarTitleKey, containerRawData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }
    }
}