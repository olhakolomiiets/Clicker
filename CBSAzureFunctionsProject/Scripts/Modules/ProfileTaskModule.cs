using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using System.Collections.Generic;
using System.Linq;
using System;

namespace CBS
{
    public class ProfileTaskModule : BaseAzureModule
    {
        private static readonly string TaskPeriodTableID = "CBSProfileTasks";

        [FunctionName(AzureFunctions.GetProfileTasksPoolMethod)]
        public static async Task<dynamic> GetProfileTasksPoolTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionProfileTasksRequest>();
            var tasksPoolID = request.TasksPoolID;

            var getResult = await GetTasksFromPoolAsync(tasksPoolID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetTasksForProfileMethod)]
        public static async Task<dynamic> GetTasksForProfileTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionProfileTasksRequest>();
            var profileID = request.ProfileID;
            var tasksPoolID = request.TasksPoolID;
            var timeZone = request.TimeZone;

            var getResult = await GetTasksForProfileAsync(profileID, tasksPoolID, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ResetProfileTasksMethod)]
        public static async Task<dynamic> ResetProfileTasksTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionProfileTasksRequest>();
            var profileID = request.ProfileID;
            var tasksPoolID = request.TasksPoolID;
            var timeZone = request.TimeZone;

            var getResult = await GetTasksForProfileAsync(profileID, tasksPoolID, timeZone, true);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddProfileTaskPointsMethod)]
        public static async Task<dynamic> AddProfileTaskPointsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionModifyProfileTasksPointsRequest>();
            var profileID = request.ProfileID;
            var tasksPoolID = request.TasksPoolID;
            var taskID = request.TaskID;
            var method = request.Method;
            var points = request.Points;
            var timeZone = request.TimeZone;

            var getResult = await ModifyTaskPointsAsync(profileID, taskID, tasksPoolID, method, points, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddProfileMuliplyTaskPointsMethod)]
        public static async Task<dynamic> AddProfileMuliplyTaskPointsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionModifyProfileMultiplyTasksPointsRequest>();
            var profileID = request.ProfileID;
            var tasksPoolID = request.TasksPoolID;
            var method = request.Method;
            var modifyPair = request.ModifyPair;
            var timeZone = request.TimeZone;

            var getResult = await ModifyMultiplyTaskPointsAsync(profileID, tasksPoolID, method, modifyPair, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PickupProfileTaskRewardMethod)]
        public static async Task<dynamic> PickupProfileTaskRewardTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionProfileTasksRequest>();
            var profileID = request.ProfileID;
            var tasksPoolID = request.TasksPoolID;
            var taskID = request.TaskID;

            var pickupResult = await PickupTaskRewardAsync(profileID, tasksPoolID, taskID);
            if (pickupResult.Error != null)
            {
                return ErrorHandler.ThrowError(pickupResult.Error).AsFunctionResult();
            }

            return pickupResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileTasksBadgeMethod)]
        public static async Task<dynamic> GetProfileTasksBadgeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionProfileTasksRequest>();
            var profileID = request.ProfileID;
            var tasksPoolID = request.TasksPoolID;

            var getResult = await GetTasksBadgeAsync(profileID, tasksPoolID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionProfileTasksResult>> GetTasksForProfileAsync(string profileID, string poolID, int timeZone, bool forceRegenerate = false)
        {
            // get tasks data
            var tasksDataResult = await GetTasksDataAsync(poolID);
            if (tasksDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionProfileTasksResult>(tasksDataResult.Error);
            }
            var tasksData = tasksDataResult.Result;
            var tasksList = tasksData.Tasks;
            var tasksCountPerPeriod = tasksData.DailyTasksCount;
            var period = tasksData.UpdatePeriod;
            if (tasksList == null || tasksList.Count == 0)
            {
                return ErrorHandler.TasksPoolNotConfigured<FunctionProfileTasksResult>();
            }

            // get available tasks
            var getSavedTasksIDs = await GetSavedTasksIDsAsync(profileID, poolID);
            if (getSavedTasksIDs.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionProfileTasksResult>(getSavedTasksIDs.Error);
            }
            var savedTasksIDs = getSavedTasksIDs.Result;
            var tasksExist = savedTasksIDs != null && savedTasksIDs.Count > 0;

            // get period state
            var periodID = ProfileTasksData.ProfileTasksTablePrefix + poolID;
            var requestDate = ServerTimeUTC.AddMilliseconds(timeZone);
            var periodStateResult = await TimePeriodAssistant.GetPeriodStateAsync(TaskPeriodTableID, profileID, periodID, period, requestDate);
            if (periodStateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionProfileTasksResult>(periodStateResult.Error);
            }
            var periodState = periodStateResult.Result;
            var periodExpired = periodState.CheckinAvailable;
            var nextResetDate = periodState.NextCheckIn;

            // generate tasks
            var needToGenerateTasks = periodExpired || !tasksExist || forceRegenerate;
            if (needToGenerateTasks)
            {
                var generateResult = await GenerateNewTasksForProfileAsync(profileID, tasksList, tasksCountPerPeriod, poolID);
                if (generateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionProfileTasksResult>(generateResult.Error);
                }
                var newTasksIDs = generateResult.Result;
                savedTasksIDs = newTasksIDs;

                // check in
                var checkInResult = await TimePeriodAssistant.CheckIn(TaskPeriodTableID, profileID, periodID, requestDate, period);
                if (checkInResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionProfileTasksResult>(checkInResult.Error);
                }
                var checkInData = checkInResult.Result;
                nextResetDate = checkInData.DateOfNextCheckIn;
            }

            // get tasks states
            var titleID = ProfileTasksData.ProfileTasksTitlePrefix + poolID;
            var profileTitleID = ProfileTasksData.ProfileTasksTitlePrefix + poolID;
            var getTasksStateResult = await TaskModule.GetEntityTasksAsync<CBSProfileTask>(profileID, CBSEntityType.PLAYER, titleID, profileTitleID, savedTasksIDs.ToArray());
            if (getTasksStateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionProfileTasksResult>(getTasksStateResult.Error);
            }
            var tasksWithStates = getTasksStateResult.Result.Tasks;

            return new ExecuteResult<FunctionProfileTasksResult>
            {
                Result = new FunctionProfileTasksResult
                {
                    Tasks = tasksWithStates,
                    NextResetDate = nextResetDate
                }
            };
        }

        public static async Task<ExecuteResult<FunctionTasksPoolResult>> GetTasksFromPoolAsync(string poolID)
        {
            var tasksDataResult = await GetTasksDataAsync(poolID);
            if (tasksDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionTasksPoolResult>(tasksDataResult.Error);
            }
            var tasksData = tasksDataResult.Result;
            return new ExecuteResult<FunctionTasksPoolResult>
            {
                Result = new FunctionTasksPoolResult
                {
                    Tasks = tasksData.Tasks
                }
            };
        }

        public static async Task<ExecuteResult<List<string>>> GetAllPoolListAsync()
        {
            var dataResult = await GetInternalTitleDataAsObjectAsync<ProfileTasksData>(TitleKeys.ProfileTasksTitleKey);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<string>>(dataResult.Error);
            }
            var tasksIDs = dataResult.Result ?? new ProfileTasksData();
            return new ExecuteResult<List<string>>
            {
                Result = tasksIDs.TasksList ?? new List<string>()
            };
        }

        public static async Task<ExecuteResult<List<string>>> GetSavedTasksIDsAsync(string profileID, string poolID)
        {
            var dataResult = await GetProfileInternalDataAsObject<ActiveProfileTasksPoolContainer>(profileID, ProfileDataKeys.ProfileTasksContainer);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<string>>(dataResult.Error);
            }
            var poolStates = dataResult.Result ?? new ActiveProfileTasksPoolContainer();
            var tasksIDs = poolStates.GetSavedForPool(poolID);
            return new ExecuteResult<List<string>>
            {
                Result = tasksIDs
            };
        }

        public static async Task<ExecuteResult<ProfilePeriodTasksData>> GetTasksDataAsync(string poolID)
        {
            var poolListResult = await GetAllPoolListAsync();
            if (poolListResult.Error != null)
            {
                return ErrorHandler.ThrowError<ProfilePeriodTasksData>(poolListResult.Error);
            }
            var poolList = poolListResult.Result;
            if (!poolList.Contains(poolID))
            {
                return ErrorHandler.TasksPoolNotFound<ProfilePeriodTasksData>();
            }

            var titleID = ProfileTasksData.ProfileTasksTitlePrefix + poolID;
            var dataResult = await GetInternalTitleDataAsObjectAsync<ProfilePeriodTasksData>(titleID);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<ProfilePeriodTasksData>(dataResult.Error);
            }
            var tasksData = dataResult.Result ?? new ProfilePeriodTasksData();
            return new ExecuteResult<ProfilePeriodTasksData>
            {
                Result = tasksData
            };
        }

        public static async Task<ExecuteResult<List<string>>> GenerateNewTasksForProfileAsync(string profileID, List<CBSProfileTask> tasksPool, int maxCount, string poolID)
        {
            // check level
            var hasLevelLimit = tasksPool.Any(x=>x.IsLockedByLevel);
            if (hasLevelLimit)
            {
                var levelInfoResult = await ProfileExpModule.GetProfileExpirienceDetailAsync(profileID);
                var level = levelInfoResult.Error == null ? levelInfoResult.Result.CurrentLevel : 0;
                tasksPool = tasksPool.Where(x=>!x.IsLockedByLevel).Concat(tasksPool.Where(x=>x.IsLockedByLevel && !x.IsLevelLocking(level))).ToList();
            }
            tasksPool = tasksPool.Where(x=>x.Weight > 0).ToList();
            var max = Math.Clamp(maxCount, 0, tasksPool.Count);
            var rnd = new Random();
            var generatedList = new List<string>();
            if (tasksPool.Count == 1)
            {
                generatedList.Add(tasksPool.FirstOrDefault().ID);
            }
            else
            {
                for (int j = 0; j < max; j++)
                {
                    int poolSize = 0;
                    for (int i = 0; i < tasksPool.Count; i++)
                    {
                        poolSize += tasksPool[i].Weight;
                    }
                    
                    // Get a random integer from 0 to PoolSize.
                    int randomNumber = rnd.Next(0, poolSize) + 1;
                    // Detect the item, which corresponds to current random number.
                    int accumulatedProbability = 0;
                    for (int i = 0; i < tasksPool.Count; i++)
                    {
                        accumulatedProbability += tasksPool[i].Weight;
                        if (randomNumber <= accumulatedProbability)
                        {
                            generatedList.Add(tasksPool[i].ID);
                            tasksPool.RemoveAt(i);
                            tasksPool.TrimExcess();
                            break;
                        }
                    }
                }
            }
            // reset all tasks states
            var titleID = ProfileTasksData.ProfileTasksTitlePrefix + poolID;
            var profileTitleID = ProfileTasksData.ProfileTasksTitlePrefix + poolID;
            var resetResult = await TaskModule.ResetAllTaskAsync(profileID, CBSEntityType.PLAYER, titleID, profileTitleID);
            if (resetResult.Error != null)
            {
                return ErrorHandler.ThrowError<ActiveProfileTasksPoolContainer>(resetResult.Error);
            }

            // save tasks
            var dataResult = await GetProfileInternalDataAsObject<ActiveProfileTasksPoolContainer>(profileID, ProfileDataKeys.ProfileTasksContainer);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<string>>(dataResult.Error);
            }
            var poolStates = dataResult.Result ?? new ActiveProfileTasksPoolContainer();
            poolStates.AddState(poolID, generatedList);

            var stateRawData = JsonPlugin.ToJsonCompress(poolStates);
            var saveResult = await SaveProfileInternalDataAsync(profileID, ProfileDataKeys.ProfileTasksContainer, stateRawData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<string>>(saveResult.Error);
            }

            return new ExecuteResult<List<string>>
            {
                Result = generatedList
            };
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<CBSTask>>> ModifyTaskPointsAsync(string profileID, string taskID, string poolID, ModifyMethod method, int points, int timeZone)
        {
            var titleID = ProfileTasksData.ProfileTasksTitlePrefix + poolID;
            var profileTitleID = ProfileTasksData.ProfileTasksTitlePrefix + poolID;

            // check if tasks is generated
            var taskDataResult = await GetTasksForProfileAsync(profileID, poolID, timeZone);
            if (taskDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(taskDataResult.Error);
            }

            var modifyRequest = new FunctionModifyTaskPointsRequest
            {
                EntityID = profileID,
                EntityType = CBSEntityType.PLAYER,
                ModifyMethod = method,
                Points = points,
                TaskID = taskID,
                TasksTitleID = titleID,
                TasksEntityTitleID = profileTitleID
            };
            var modifyResult = await TaskModule.ModifyTaskPointAsync<CBSTask>(modifyRequest);
            if (modifyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(modifyResult.Error);
            }
            return modifyResult;
        }

        public static async Task<ExecuteResult<FunctionModifyMultiplyTaskResult<CBSTask>>> ModifyMultiplyTaskPointsAsync(string profileID, string poolID, ModifyMethod method, Dictionary<string, int> tasksPair, int timeZone)
        {
            var titleID = ProfileTasksData.ProfileTasksTitlePrefix + poolID;
            var profileTitleID = ProfileTasksData.ProfileTasksTitlePrefix + poolID;

            // check if tasks is generated
            var taskDataResult = await GetTasksForProfileAsync(profileID, poolID, timeZone);
            if (taskDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyMultiplyTaskResult<CBSTask>>(taskDataResult.Error);
            }

            var modifyRequest = new FunctionModifyMultiplyTaskPointsRequest
            {
                EntityID = profileID,
                EntityType = CBSEntityType.PLAYER,
                ModifyMethod = method,
                ModifyPair = tasksPair,
                TasksTitleID = titleID,
                TasksEntityTitleID = profileTitleID
            };
            var modifyResult = await TaskModule.ModifyMultiplyTaskPointAsync<CBSTask>(modifyRequest);
            if (modifyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyMultiplyTaskResult<CBSTask>>(modifyResult.Error);
            }
            return modifyResult;
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<CBSTask>>> PickupTaskRewardAsync(string profileID, string tasksPoolID, string taskID)
        {
            // get available tasks
            var getSavedTasksIDs = await GetSavedTasksIDsAsync(profileID, tasksPoolID);
            if (getSavedTasksIDs.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(getSavedTasksIDs.Error);
            }
            var savedTasksIDs = getSavedTasksIDs.Result ?? new List<string>();

            var titleID = ProfileTasksData.ProfileTasksTitlePrefix + tasksPoolID;
            var profileTitleID = ProfileTasksData.ProfileTasksTitlePrefix + tasksPoolID;
            var pickupResult = await TaskModule.PickupTaskRewardAsync<CBSTask>(profileID, CBSEntityType.PLAYER, titleID, profileTitleID, taskID, savedTasksIDs.ToArray());
            if (pickupResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(pickupResult.Error);
            }
            return pickupResult;
        }

        public static async Task<ExecuteResult<FunctionBadgeResult>> GetTasksBadgeAsync(string profileID, string tasksPoolID)
        {
            // get available tasks
            var getSavedTasksIDs = await GetSavedTasksIDsAsync(profileID, tasksPoolID);
            if (getSavedTasksIDs.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBadgeResult>(getSavedTasksIDs.Error);
            }
            var savedTasksIDs = getSavedTasksIDs.Result;
            var tasksExist = savedTasksIDs != null && savedTasksIDs.Count > 0;

            if (tasksExist)
            {
                // get tasks states
                var titleID = ProfileTasksData.ProfileTasksTitlePrefix + tasksPoolID;
                var profileTitleID = ProfileTasksData.ProfileTasksTitlePrefix + tasksPoolID;
                var getTasksStateResult = await TaskModule.GetEntityTasksAsync<CBSTask>(profileID, CBSEntityType.PLAYER, titleID, profileTitleID, savedTasksIDs.ToArray());
                if (getTasksStateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionBadgeResult>(getTasksStateResult.Error);
                }
                var tasksWithStates = getTasksStateResult.Result.Tasks ?? new List<CBSTask>();
                var notRewardedTasks = tasksWithStates.Where(x=>x.Type != TaskType.TIERED && x.IsComplete && x.IsRewardAvailable());
                var notRewardedTieredTasks = tasksWithStates.Where(x=>x.Type == TaskType.TIERED && x.IsRewardAvailable());
                var notRewardedCount = notRewardedTasks.Count() + notRewardedTieredTasks.Count();

                return new ExecuteResult<FunctionBadgeResult>
                {
                    Result = new FunctionBadgeResult
                    {
                        Count = notRewardedCount
                    }
                };
            }
            else
            {
                return new ExecuteResult<FunctionBadgeResult>
                {
                    Result = new FunctionBadgeResult
                    {
                        Count = 0
                    }
                };
            }
        }
    }
}