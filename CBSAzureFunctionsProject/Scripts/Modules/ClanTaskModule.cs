using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using System.Collections.Generic;
using Medallion.Threading.Azure;
using Medallion.Threading;
using System.Linq;
using System;

namespace CBS
{
    public class ClanTaskModule : BaseAzureModule
    {
        private static readonly string LockIDPrefix = "clantask"; 
        private static readonly string TaskPeriodTableID = "CBSClanTasks";
        private static readonly string TaskDefaultPoolID = "ClanTaskPool";

        [FunctionName(AzureFunctions.GetTasksForClanMethod)]
        public static async Task<dynamic> GetTasksForClanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanTasksRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;

            var getResult = await GetTasksForClanAsync(clanID, TaskDefaultPoolID, false, profileID : profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ResetClanTasksMethod)]
        public static async Task<dynamic> ResetClanTasksTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanTasksRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;

            var getResult = await GetTasksForClanAsync(clanID, TaskDefaultPoolID, true, profileID : profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddClanTaskPointsMethod)]
        public static async Task<dynamic> AddClanTaskPointsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionModifyClanTasksPointsRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var taskID = request.TaskID;
            var method = request.Method;
            var points = request.Points;

            var getResult = await ModifyTaskPointsAsync(clanID, taskID, TaskDefaultPoolID, method, points, profileID, log);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionClanTasksResult>> GetTasksForClanAsync(string clanID, string taskPoolID, bool forceRegenerate = false, string profileID = null)
        {
            // check is profile exist in clan
            if (!string.IsNullOrEmpty(profileID))
            {
                var getProfileClanIDResult = await ProfileModule.GetProfileClanIDAsync(profileID);
                if (getProfileClanIDResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTasksResult>(getProfileClanIDResult.Error);
                }
                var profileClanID = getProfileClanIDResult.Result;
                if (string.IsNullOrEmpty(profileClanID))
                {
                    return ErrorHandler.ProfileIsNotMemberOfClan<FunctionClanTasksResult>();
                }
                if (profileClanID != clanID)
                {
                    return ErrorHandler.InvalidInput<FunctionClanTasksResult>();
                }
            }

            // get tasks data
            var tasksDataResult = await GetTasksDataAsync();
            if (tasksDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionClanTasksResult>(tasksDataResult.Error);
            }
            var tasksData = tasksDataResult.Result;
            var tasksList = tasksData.Tasks;
            var tasksCountPerPeriod = tasksData.DailyTasksCount;
            var period = tasksData.UpdatePeriod;
            if (tasksList == null || tasksList.Count == 0)
            {
                return ErrorHandler.TasksPoolNotConfigured<FunctionClanTasksResult>();
            }

            // distributed lock by entity id
            var lockID = LockIDPrefix + clanID;
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(lockID))
            {
                await using var handle = await locker.TryAcquireLockAsync(lockID);  
                // get available tasks
                var getSavedTasksIDs = await GetSavedTasksIDsAsync(clanID, taskPoolID);
                if (getSavedTasksIDs.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTasksResult>(getSavedTasksIDs.Error);
                }
                var savedTasksIDs = getSavedTasksIDs.Result;
                var tasksExist = savedTasksIDs != null && savedTasksIDs.Count > 0;

                // get period state
                var periodID = ClanTaskData.ClanTasksTablePrefix + taskPoolID;
                var requestDate = ServerTimeUTC.AddMilliseconds(ServerTimezoneOffset);
                var periodStateResult = await TimePeriodAssistant.GetPeriodStateAsync(TaskPeriodTableID, clanID, periodID, period, requestDate);
                if (periodStateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTasksResult>(periodStateResult.Error);
                }
                var periodState = periodStateResult.Result;
                var periodExpired = periodState.CheckinAvailable;
                var nextResetDate = periodState.NextCheckIn;

                // generate tasks
                var needToGenerateTasks = periodExpired || !tasksExist || forceRegenerate;
                if (needToGenerateTasks)
                {
                    var generateResult = await GenerateNewTasksForClanAsync(clanID, tasksList, tasksCountPerPeriod, taskPoolID);
                    if (generateResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionClanTasksResult>(generateResult.Error);
                    }
                    var newTasksIDs = generateResult.Result;
                    savedTasksIDs = newTasksIDs;

                    // check in
                    var checkInResult = await TimePeriodAssistant.CheckIn(TaskPeriodTableID, clanID, periodID, requestDate, period);
                    if (checkInResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionClanTasksResult>(checkInResult.Error);
                    }
                    var checkInData = checkInResult.Result;
                    nextResetDate = checkInData.DateOfNextCheckIn;                            
                }

                // get tasks states
                //var titleID = ClanTaskData.ClanTasksTitlePrefix + taskPoolID;
                var titleID = TitleKeys.ClanTaskTitleKey;
                var profileTitleID = ClanTaskData.ClanTasksTitlePrefix + taskPoolID;
                var getTasksStateResult = await TaskModule.GetEntityTasksAsync<CBSClanTask>(clanID, CBSEntityType.CLAN, titleID, profileTitleID, savedTasksIDs.ToArray());
                if (getTasksStateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTasksResult>(getTasksStateResult.Error);
                }
                var tasksWithStates = getTasksStateResult.Result.Tasks;

                return new ExecuteResult<FunctionClanTasksResult>
                {
                    Result = new FunctionClanTasksResult
                    {
                        Tasks = tasksWithStates,
                        NextResetDate = nextResetDate
                    }
                };
            }
        }

        public static async Task<ExecuteResult<ClanTaskData>> GetTasksDataAsync()
        {
            var dataResult = await GetInternalTitleDataAsObjectAsync<ClanTaskData>(TitleKeys.ClanTaskTitleKey);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<ClanTaskData>(dataResult.Error);
            }
            var tasksData = dataResult.Result ?? new ClanTaskData();
            return new ExecuteResult<ClanTaskData>
            {
                Result = tasksData
            };
        }

        public static async Task<ExecuteResult<List<string>>> GetSavedTasksIDsAsync(string clanID, string poolID)
        {
            var dataResult = await GetProfileInternalDataAsObject<ActiveProfileTasksPoolContainer>(clanID, ProfileDataKeys.ProfileTasksContainer);
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

        public static async Task<ExecuteResult<List<string>>> GenerateNewTasksForClanAsync(string clanID, List<CBSClanTask> tasksPool, int maxCount, string poolID)
        {
            // check level
            var hasLevelLimit = tasksPool.Any(x=>x.IsLockedByLevel);
            if (hasLevelLimit)
            {
                var levelInfoResult = await ClanExpModule.GetClanExpirienceDetailAsync(clanID);
                var level = levelInfoResult.Error == null ? levelInfoResult.Result.CurrentLevel : 0;
                tasksPool = tasksPool.Where(x=>!x.IsLockedByLevel).Concat(tasksPool.Where(x=>x.IsLockedByLevel && !x.IsLevelLocking(level))).ToList();
            }
            tasksPool = tasksPool.Where(x=>x.Weight > 0).ToList();
            var max = Math.Clamp(maxCount, 0, tasksPool.Count);
            var rnd = new Random();
            var generatedList = new List<string>();
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
            // reset all tasks states
            //var titleID = ClanTaskData.ClanTasksTitlePrefix + poolID;
            var titleID = TitleKeys.ClanTaskTitleKey;
            var clanTitleID = ClanTaskData.ClanTasksTitlePrefix + poolID;
            var resetResult = await TaskModule.ResetAllTaskAsync(clanID, CBSEntityType.CLAN, titleID, clanTitleID);
            if (resetResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<string>>(resetResult.Error);
            }

            // save tasks
            var dataResult = await GetProfileInternalDataAsObject<ActiveProfileTasksPoolContainer>(clanID, ProfileDataKeys.ProfileTasksContainer);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<string>>(dataResult.Error);
            }
            var poolStates = dataResult.Result ?? new ActiveProfileTasksPoolContainer();
            poolStates.AddState(poolID, generatedList);

            var stateRawData = JsonPlugin.ToJsonCompress(poolStates);
            var saveResult = await SaveProfileInternalDataAsync(clanID, ProfileDataKeys.ProfileTasksContainer, stateRawData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<string>>(saveResult.Error);
            }

            return new ExecuteResult<List<string>>
            {
                Result = generatedList
            };
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<CBSClanTask>>> ModifyTaskPointsAsync(string clanID, string taskID, string poolID, ModifyMethod method, int points, string profileID = null, ILogger log = null)
        {
            // check profile permission
            if (!string.IsNullOrEmpty(profileID))
            {
                var hasPermissionResult = await ClanModule.HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.ADD_TASKS_POINTS);
                if (hasPermissionResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSClanTask>>(hasPermissionResult.Error);
                }
                var hasPermission = hasPermissionResult.Result.Value;
                if (!hasPermission)
                {
                    return ErrorHandler.NotEnoughRights<FunctionModifyTaskResult<CBSClanTask>>();
                }
            }

            // check if tasks is generated
            var taskDataResult = await GetTasksDataAsync();
            if (taskDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSClanTask>>(taskDataResult.Error);
            }
            var period = taskDataResult.Result.UpdatePeriod;

            var periodID = ClanTaskData.ClanTasksTablePrefix + poolID;
            var requestDate = ServerTimeUTC.AddMilliseconds(ServerTimezoneOffset);
            var periodStateResult = await TimePeriodAssistant.GetPeriodStateAsync(TaskPeriodTableID, clanID, periodID, period, requestDate);
            if (periodStateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSClanTask>>(periodStateResult.Error);
            }
            var periodState = periodStateResult.Result;
            var periodExpired = periodState.CheckinAvailable;
            if (periodExpired)
            {
                var regenerateResult = await GetTasksForClanAsync(clanID, poolID, false, profileID);
                if (regenerateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSClanTask>>(regenerateResult.Error);
                }
            }

            //var titleID = ProfileTasksData.ProfileTasksTitlePrefix + poolID;
            var titleID = TitleKeys.ClanTaskTitleKey;
            var clanTitleID = ClanTaskData.ClanTasksTitlePrefix + poolID;

            var modifyRequest = new FunctionModifyTaskPointsRequest
            {
                EntityID = clanID,
                EntityType = CBSEntityType.CLAN,
                ModifyMethod = method,
                Points = points,
                TaskID = taskID,
                TasksTitleID = titleID,
                TasksEntityTitleID = clanTitleID
            };
            var modifyResult = await TaskModule.ModifyTaskPointAsync<CBSClanTask>(modifyRequest);
            if (modifyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSClanTask>>(modifyResult.Error);
            }
            return modifyResult;
        }

        public static void RewardForCompleteTask(string clanID, CBSClanTask task)
        {
            if (task == null)
                return;
            Task.Run( async ()=>{
                await RewardForCompleteTaskAsync(clanID, task);
            });
        }

        public static async Task RewardForCompleteTaskAsync(string clanID, CBSClanTask task)
        {
            var clanTaskDataResult = await GetTasksDataAsync();
            if (clanTaskDataResult.Error != null)
                return;
            var tasksData = clanTaskDataResult.Result;
            var grantType = tasksData.RewardBehavior;
            var rewardDelivery = tasksData.RewardDelivery ?? new RewardDelivery();
            var sendToInbox = rewardDelivery.DeliveryType == RewardDeliveryType.SEND_TO_INBOX;
            if (grantType == TaskRewardBehavior.FOR_EACH_TASK)
            {
                if (task == null)
                    return;
                var clanReward = task.GetNotRewardedObject();

                if (clanReward != null && !clanReward.IsEmpty())
                {
                    await RewardModule.GrantRewardToProfileAsync(clanReward, clanID);
                }   

                var profileReward = task.GetNotRewardedObjectForProfile();
                if (profileReward.IsEmpty())
                    return;
                    
                var getClanMembersResult = await ClanModule.GetClanMembersAsync(clanID, new CBSProfileConstraints());
                if (getClanMembersResult.Error == null)
                {
                    var clanMembers = getClanMembersResult.Result.Members ?? new List<ClanMember>();
                    foreach (var member in clanMembers)
                    {
                        if (sendToInbox)
                        {
                            // send reward to inbox
                            var notification = CBSNotification.FromRewardDelivery(rewardDelivery, profileReward);
                            await NotificationModule.SendNotificationAsync(notification, member.ProfileID);
                        }
                        else
                        {
                            await RewardModule.GrantRewardToProfileAsync(profileReward, member.ProfileID);
                        }
                    }
                }
            }
            else if (grantType == TaskRewardBehavior.WHEN_COMPLETE_ALL)
            {
                var getClanTasksResult = await GetTasksForClanAsync(clanID, TaskDefaultPoolID);
                if (getClanTasksResult.Error != null)
                    return;
                var clanTasks = getClanTasksResult.Result.Tasks ?? new List<CBSClanTask>();
                var allCompleted = !clanTasks.Any(x=>x.IsComplete == false);
                if (!allCompleted)
                    return;
                RewardObject clanReward = new RewardObject();
                RewardObject profileReward = new RewardObject();
                foreach (var clanTask in clanTasks)
                {
                    clanTask.MarkAsNotRewarded();
                    clanReward = clanReward.MergeReward(clanTask.GetNotRewardedObject());
                    profileReward = profileReward.MergeReward(clanTask.GetNotRewardedObjectForProfile());
                }

                await RewardModule.GrantRewardToProfileAsync(clanReward, clanID);

                if (profileReward.IsEmpty())
                    return;
                var getClanMembersResult = await ClanModule.GetClanMembersAsync(clanID, new CBSProfileConstraints());
                if (getClanMembersResult.Error == null)
                {
                    var clanMembers = getClanMembersResult.Result.Members ?? new List<ClanMember>();
                    foreach (var member in clanMembers)
                    {
                        if (sendToInbox)
                        {
                            // send reward to inbox
                            var notification = CBSNotification.FromRewardDelivery(rewardDelivery, profileReward);
                            await NotificationModule.SendNotificationAsync(notification, member.ProfileID);
                        }
                        else
                        {
                            await RewardModule.GrantRewardToProfileAsync(profileReward, member.ProfileID);
                        }
                    }
                }
            }
        }

        public static void ExecuteClanTaskEvents(string clanID, CBSClanTask task)
        {
            if (task == null)
                return;
            Task.Run(async ()=>{ await ExecuteClanTaskEventsAsync(clanID, task); });
        }

        public static async Task ExecuteClanTaskEventsAsync(string clanID, CBSClanTask task)
        {
            var clanEvents = task.GetClanEvents();
            EventModule.ExecuteClanEventContainer(clanID, clanEvents);
            var profileEvents = task.GetEvents();

            if (profileEvents != null)
            {
                var getClanMembersResult = await ClanModule.GetClanMembersAsync(clanID, new CBSProfileConstraints());
                if (getClanMembersResult.Error == null)
                {
                    var clanMembers = getClanMembersResult.Result.Members ?? new List<ClanMember>();
                    foreach (var member in clanMembers)
                    {
                        EventModule.ExecuteProfileEventContainer(member.ProfileID, profileEvents);
                    }
                }
            }
        }
    }
}