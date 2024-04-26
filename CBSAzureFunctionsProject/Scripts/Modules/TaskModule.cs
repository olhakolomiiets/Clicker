using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using CBS.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using Medallion.Threading;
using Medallion.Threading.Azure;

namespace CBS
{
    public class TaskModule : BaseAzureModule
    {
        private static readonly string LockIDPrefix = "task"; 

        public static async Task<ExecuteResult<FunctionTasksResult<T>>> GetEntityTasksAsync<T>(string entityID, CBSEntityType type, string tasksDataKey, string entityTaskDataKey, string [] takskIDs = null) where T : CBSTask
        {
            var dataResult = await GetTasksDataAsync<T>(tasksDataKey);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionTasksResult<T>>(dataResult.Error);
            }
            var tasksData = dataResult.Result;
            var tasksList = tasksData.Tasks ?? new List<T>();
            if (takskIDs != null)
            {
                tasksList = tasksList.Where(x=> takskIDs.Contains(x.ID)).ToList();
            }
            
            // get entity level 
            var expResult = await ExpirienceModule.GetExpirienceDetailOfEntityAsync(type, entityID);
            var level = expResult.Error != null ? 0 : expResult.Result?.CurrentLevel;
            
            // get entity tasks data
            var entityTasksStateResult = await GetEntityTasksStateAsync(entityID, type, entityTaskDataKey);
            if (entityTasksStateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionTasksResult<T>>(entityTasksStateResult.Error);
            }
            var entityTaksResult = entityTasksStateResult.Result ?? new Dictionary<string, BaseTaskState>();

            // set tasks data
            foreach (var task in tasksList) {
                var newTaskState = task.ApplyPlayerState(entityTaksResult, (int)level);
                task.UpdateState(newTaskState);
            };
	
            return new ExecuteResult<FunctionTasksResult<T>>
            {
                Result = new FunctionTasksResult<T>
                {
                    Tasks = tasksList,
                    EntityLevel = (int)level,
                    TasksStates = entityTaksResult,
                    AutoReward = tasksData.AutomaticReward
                }
            };
        }

        public static async Task<ExecuteResult<CBSBaseTasksData<T>>> GetTasksDataAsync<T>(string tasksDataKey) where T : CBSTask
        {
            var getTableResult = await GetInternalTitleDataAsObjectAsync<CBSBaseTasksData<T>>(tasksDataKey);
            if (getTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<CBSBaseTasksData<T>>(getTableResult.Error);
            }
            var tasksData = getTableResult.Result;
            if (tasksData == null)
            {
                return ErrorHandler.TasksPoolNotConfigured<CBSBaseTasksData<T>>();
            }

            return new ExecuteResult<CBSBaseTasksData<T>>
            {
                Result = tasksData
            };
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<T>>> ModifyTaskPointAsync<T>(FunctionModifyTaskPointsRequest request) where T : CBSTask
        {
            var entityID = request.EntityID;
            var entityType = request.EntityType;
            var method = request.ModifyMethod;
            var points = request.Points;
            var taskID = request.TaskID;
            var tasksTitleID = request.TasksTitleID;
            var tasksEntityID = request.TasksEntityTitleID;
            var postClanRewardProcess = false;

            // distributed lock by entity id
            var lockID = LockIDPrefix + entityID;
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(lockID))
            {
                await using var handle = await locker.TryAcquireLockAsync(entityID);       
                var getTasksDataResult = await GetEntityTasksAsync<T>(entityID, entityType, tasksTitleID, tasksEntityID);
                if (getTasksDataResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionModifyTaskResult<T>>(getTasksDataResult.Error);
                }
                var tasksResult = getTasksDataResult.Result;
                var tasks = tasksResult.Tasks;
                var entityLevel = tasksResult.EntityLevel;
                var taskStates = tasksResult.TasksStates;
                var autoReward = tasksResult.AutoReward || entityType == CBSEntityType.CLAN;

                var task = tasks.FirstOrDefault(x=>x.ID == taskID);
                if (task == null)
                {
                    return ErrorHandler.TaskIDNotFound<FunctionModifyTaskResult<T>>();
                }
                var isLockedByLevel = task.IsLockedByLevel;
                var lockLevel = task.LockLevel;
                var tierIndex = task.TierIndex;
                
                // check complete
                var isComplete = task.IsComplete;
                if (isComplete)
                {
                    return ErrorHandler.TaskAlreadyCompleted<FunctionModifyTaskResult<T>>();
                }

                var rewarded = task.Rewarded;
                var available = true;
                
                var taskState = task.AddPoints(points, method);
                isComplete = taskState.IsComplete;
                var isCompleteTier = !isComplete && tierIndex != taskState.TierIndex;

                // check available
                if (isLockedByLevel)
                {
                    available = !task.IsLevelLocking(entityLevel);
                }
                
                // reward
                GrantRewardResult rewardResult = null;
                var justRewarded = false;
                if (autoReward && (!rewarded && isComplete || isCompleteTier || isComplete && task.Type == TaskType.TIERED))
                {
                    var reward = task.GetNotRewardedObject();               
                    // grant reward
                    if (entityType == CBSEntityType.CLAN)
                    {
                        postClanRewardProcess = true;
                    }
                    else
                    {
                        var grantResult = await GrantTaskRewardAsync<T>(entityID, tasksTitleID, entityType, reward);
                        if (grantResult.Error != null)
                        {
                            return ErrorHandler.ThrowError<FunctionModifyTaskResult<T>>(grantResult.Error);
                        }
                        rewardResult = grantResult.Result;  
                    }            
                    if (task.Type == TaskType.TIERED)
                    {
                        taskState.MarkRewardsAsGranted();
                    }
                    taskState.Rewarded = true;
                    justRewarded = true;
                }
                var revicedReward = justRewarded ? rewardResult?.OriginReward : null;

                // events
                if (isComplete || isCompleteTier)
                {
                    var profileEvents = task.GetEvents();
                    // execute profile events
                    if (profileEvents != null)
                    {
                        EventModule.ExecuteProfileEventContainer(entityID, profileEvents);
                    }
                    if (entityType == CBSEntityType.CLAN)
                    {
                        ClanTaskModule.ExecuteClanTaskEvents(entityID, task as CBSClanTask);
                    }
                }

                // save state
                taskState.IsAvailable = available;
                task.UpdateState(taskState);         
                taskStates[taskID] = taskState;
                var saveResult = await SaveTasksStatesAsync(entityID, entityType, tasksEntityID, taskStates);
                if (saveResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionModifyTaskResult<T>>(saveResult.Error);
                }

                if (postClanRewardProcess)
                {
                    var newInstance = task.Copy<CBSClanTask>();
                    newInstance.MarkAsNotRewarded();
                    ClanTaskModule.RewardForCompleteTask(entityID, newInstance);
                }
                
                return new ExecuteResult<FunctionModifyTaskResult<T>>
                {
                    Result = new FunctionModifyTaskResult<T>
                    {
                        Task = task,
                        CompleteTask = isComplete,
                        CompleteTier = isCompleteTier,
                        RewardResult = rewardResult
                    }
                };    
            }                    
        }

        public static async Task<ExecuteResult<FunctionModifyMultiplyTaskResult<T>>> ModifyMultiplyTaskPointAsync<T>(FunctionModifyMultiplyTaskPointsRequest request) where T : CBSTask
        {
            var entityID = request.EntityID;
            var entityType = request.EntityType;
            var method = request.ModifyMethod;
            var modifyPair = request.ModifyPair;
            var tasksTitleID = request.TasksTitleID;
            var tasksEntityID = request.TasksEntityTitleID;

            List<Action> postUpdateAction = new List<Action>();
            Dictionary<string, FunctionModifyTaskResult<T>> tasksResults = new Dictionary<string, FunctionModifyTaskResult<T>>();

            // distributed lock by entity id
            var lockID = LockIDPrefix + entityID;
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(lockID))
            {
                await using var handle = await locker.TryAcquireLockAsync(entityID);       
                var getTasksDataResult = await GetEntityTasksAsync<T>(entityID, entityType, tasksTitleID, tasksEntityID);
                if (getTasksDataResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionModifyTaskResult<T>>(getTasksDataResult.Error);
                }
                var postClanRewardProcess = false;
                var tasksResult = getTasksDataResult.Result;
                var tasks = tasksResult.Tasks;
                var entityLevel = tasksResult.EntityLevel;
                var taskStates = tasksResult.TasksStates;
                var autoReward = tasksResult.AutoReward || entityType == CBSEntityType.CLAN;

                var modifyCount = modifyPair.Count;
                for (int i=0;i<modifyCount;i++)
                {
                    var pair = modifyPair.ElementAt(i);
                    var taskID = pair.Key;
                    var points = pair.Value;

                    var task = tasks.FirstOrDefault(x=>x.ID == taskID);
                    if (task == null)
                    {
                        //tasksResults[taskID] = ErrorHandler.TaskIDNotFound<FunctionModifyMultiplyTaskResult<T>>();
                        continue;
                    }
                    var isLockedByLevel = task.IsLockedByLevel;
                    var lockLevel = task.LockLevel;
                    var tierIndex = task.TierIndex;
                    
                    // check complete
                    var isComplete = task.IsComplete;
                    if (isComplete)
                    {
                        //tasksResults[taskID] = ErrorHandler.TaskAlreadyCompleted<FunctionModifyMultiplyTaskResult<T>>();
                        continue;
                    }

                    var rewarded = task.Rewarded;
                    var available = true;
                    
                    var taskState = task.AddPoints(points, method);
                    isComplete = taskState.IsComplete;
                    var isCompleteTier = !isComplete && tierIndex != taskState.TierIndex;

                    // check available
                    if (isLockedByLevel)
                    {
                        available = !task.IsLevelLocking(entityLevel);
                    }
                    
                    // reward
                    GrantRewardResult rewardResult = null;
                    var justRewarded = false;
                    if (autoReward && (!rewarded && isComplete || isCompleteTier || isComplete && task.Type == TaskType.TIERED))
                    {
                        var reward = task.GetNotRewardedObject();               
                        // grant reward
                        if (entityType == CBSEntityType.CLAN)
                        {
                            postClanRewardProcess = true;
                        }
                        else
                        {
                            var grantResult = await GrantTaskRewardAsync<T>(entityID, tasksTitleID, entityType, reward);
                            if (grantResult.Error != null)
                            {
                                //tasksResults[taskID] = ErrorHandler.ThrowError<FunctionModifyMultiplyTaskResult<T>>(grantResult.Error);
                                continue;
                            }
                            rewardResult = grantResult.Result;  
                        }            
                        if (task.Type == TaskType.TIERED)
                        {
                            taskState.MarkRewardsAsGranted();
                        }
                        taskState.Rewarded = true;
                        justRewarded = true;
                    }
                    var revicedReward = justRewarded ? rewardResult?.OriginReward : null;

                    // events
                    if (isComplete || isCompleteTier)
                    {
                        var profileEvents = task.GetEvents();
                        // execute profile events
                        if (profileEvents != null)
                        {
                            EventModule.ExecuteProfileEventContainer(entityID, profileEvents);
                        }
                        if (entityType == CBSEntityType.CLAN)
                        {
                            ClanTaskModule.ExecuteClanTaskEvents(entityID, task as CBSClanTask);
                        }
                    }

                    // apply state
                    taskState.IsAvailable = available;
                    task.UpdateState(taskState);         
                    taskStates[taskID] = taskState;

                    if (postClanRewardProcess)
                    {
                        var newInstance = task.Copy<CBSClanTask>();
                        newInstance.MarkAsNotRewarded();
                        postUpdateAction.Add(()=>
                        {
                            ClanTaskModule.RewardForCompleteTask(entityID, newInstance);
                        });         
                    }

                    tasksResults[taskID] = new FunctionModifyTaskResult<T>
                    {
                        Task = task,
                        CompleteTask = isComplete,
                        CompleteTier = isCompleteTier,
                        RewardResult = rewardResult
                    };
                }

                

                // save state
                var saveResult = await SaveTasksStatesAsync(entityID, entityType, tasksEntityID, taskStates);
                if (saveResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionModifyTaskResult<T>>(saveResult.Error);
                }

                foreach (var postAction in postUpdateAction)
                    postAction?.Invoke();
                
                return new ExecuteResult<FunctionModifyMultiplyTaskResult<T>>
                {
                    Result = new FunctionModifyMultiplyTaskResult<T>
                    {
                        TasksResults = tasksResults
                    }
                };    
            }                    
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<T>>> PickupTaskRewardAsync<T>(string entityID, CBSEntityType entityType, string tasksTitleID, string tasksEntityID, string taskID, string [] fromTakskIDs = null) where T : CBSTask
        {            
            var getTasksDataResult = await GetEntityTasksAsync<T>(entityID, entityType, tasksTitleID, tasksEntityID, fromTakskIDs);
            if (getTasksDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<T>>(getTasksDataResult.Error);
            }
            var tasksResult = getTasksDataResult.Result;
            var tasks = tasksResult.Tasks;
            var entityLevel = tasksResult.EntityLevel;
            var taskStates = tasksResult.TasksStates ?? new Dictionary<string, BaseTaskState>();
            var autoReward = tasksResult.AutoReward;
            var taskState = taskStates.ContainsKey(taskID) ? taskStates[taskID] : new BaseTaskState();

            var task = tasks.FirstOrDefault(x=>x.ID == taskID);
            if (task == null)
            {
                return ErrorHandler.TaskIDNotFound<FunctionModifyTaskResult<T>>();
            }
            var taskType = task.Type;
            var isRewarded = task.Rewarded;
            var reward = task.GetNotRewardedObject();
            if (taskType == TaskType.TIERED)
            {
                isRewarded = reward.IsEmpty();
            }

            if (isRewarded)
            {
                return ErrorHandler.TaskAlreadyRewarded<FunctionModifyTaskResult<T>>();
            }

            var isComplete = task.IsComplete;
            if (taskType == TaskType.TIERED)
            {              
                taskState.MarkRewardsAsGranted();
            } 
            else
            {
                if (!isComplete)
                {
                    return ErrorHandler.TaskNotComplete<FunctionModifyTaskResult<T>>();
                }  
            }   
            
            var grantResult = await GrantTaskRewardAsync<T>(entityID, tasksTitleID, entityType, reward);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<T>>(grantResult.Error);
            }
            var rewardResult = grantResult.Result;

            // save state
            taskState.IsComplete = isComplete;
            taskState.Rewarded = true;
            task.UpdateState(taskState);
            taskStates[taskID] = taskState;

            var saveResult = await SaveTasksStatesAsync(entityID, entityType, tasksEntityID, taskStates);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<T>>(saveResult.Error);
            }
            
            return new ExecuteResult<FunctionModifyTaskResult<T>>
            {
                Result = new FunctionModifyTaskResult<T>
                {
                    Task = task,
                    CompleteTask = isComplete,
                    RewardResult = rewardResult
                }
            };
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<T>>> ResetTaskAsync<T>(string entityID, CBSEntityType entityType, string tasksTitleID, string tasksEntityID, string taskID) where T : CBSTask
        {
            var getTasksDataResult = await GetEntityTasksAsync<T>(entityID, entityType, tasksTitleID, tasksEntityID);
            if (getTasksDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<T>>(getTasksDataResult.Error);
            }
            var tasksResult = getTasksDataResult.Result;
            var tasks = tasksResult.Tasks;
            var entityLevel = tasksResult.EntityLevel;
            var taskStates = tasksResult.TasksStates ?? new Dictionary<string, BaseTaskState>();
            var autoReward = tasksResult.AutoReward;
            var taskState = taskStates.ContainsKey(taskID) ? taskStates[taskID] : new BaseTaskState();

            var task = tasks.FirstOrDefault(x=>x.ID == taskID);
            if (task == null)
            {
                return ErrorHandler.TaskIDNotFound<FunctionModifyTaskResult<T>>();
            }

            // save state
            taskState.IsComplete = false;
            taskState.Rewarded = false;
            taskState.CurrentStep = 0;
            taskState.TierIndex = 0;
            taskState.GrantedRewards = null;
            task.UpdateState(taskState);
            taskStates[taskID] = taskState;

            var saveResult = await SaveTasksStatesAsync(entityID, entityType, tasksEntityID, taskStates);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<T>>(saveResult.Error);
            }
            
            return new ExecuteResult<FunctionModifyTaskResult<T>>
            {
                Result = new FunctionModifyTaskResult<T>
                {
                    Task = task,
                    CompleteTask = false,
                    RewardResult = null
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ResetAllTaskAsync(string entityID, CBSEntityType entityType, string tasksTitleID, string tasksEntityID)
        {
            var taskStates = new Dictionary<string, BaseTaskState>();

            var saveResult = await SaveTasksStatesAsync(entityID, entityType, tasksEntityID, taskStates);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
            }
            
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        // internal

        private static async Task<ExecuteResult<FunctionEmptyResult>> SaveTasksStatesAsync(string entityID, CBSEntityType entityType, string tasksEntityID, Dictionary<string, BaseTaskState> states)
        {
            if (entityType == CBSEntityType.PLAYER)
            {
                var entityTasksRawData = JsonPlugin.ToJsonCompress(states);
                var saveResult = await SaveProfileInternalDataAsync(entityID, tasksEntityID, entityTasksRawData);
                if (saveResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
                }
            }
            else if (entityType == CBSEntityType.CLAN)
            {
                var saveResult = await TableClanAssistant.UpdateClanTasksStateAsync(entityID, states);
                if (saveResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
                }
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        private static async Task<ExecuteResult<Dictionary<string, BaseTaskState>>> GetEntityTasksStateAsync(string entityID, CBSEntityType type, string entityDataKey)
        {
            if (type == CBSEntityType.PLAYER)
            {
                var dataResult = await GetProfileInternalDataAsObject<Dictionary<string, BaseTaskState>>(entityID, entityDataKey);
                if (dataResult.Error != null)
                {
                    return ErrorHandler.ThrowError<Dictionary<string, BaseTaskState>>(dataResult.Error);
                }
                var dataState = dataResult.Result;
                return new ExecuteResult<Dictionary<string, BaseTaskState>>
                {
                    Result = dataState
                };
            }
            else if (type == CBSEntityType.CLAN)
            {
                var dataResult = await TableClanAssistant.GetClanTaskState(entityID);
                if (dataResult.Error != null)
                {
                    return ErrorHandler.ThrowError<Dictionary<string, BaseTaskState>>(dataResult.Error);
                }
                var dataState = dataResult.Result;
                return new ExecuteResult<Dictionary<string, BaseTaskState>>
                {
                    Result = dataState
                };
            }
            return new ExecuteResult<Dictionary<string, BaseTaskState>>();
        }

        private static async Task<ExecuteResult<GrantRewardResult>> GrantTaskRewardAsync<T>(string entityID, string tasksTitleID, CBSEntityType entityType, RewardObject reward) where T : CBSTask    
        {
            var dataResult = await GetTasksDataAsync<T>(tasksTitleID);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<GrantRewardResult>(dataResult.Error);
            }
            var tasksData = dataResult.Result;
            var rewardDelivery = tasksData.RewardDelivery ?? new RewardDelivery();

            if (entityType == CBSEntityType.PLAYER)
            {
                var sendToInbox = tasksData.AutomaticReward && rewardDelivery.DeliveryType == RewardDeliveryType.SEND_TO_INBOX;
                if (sendToInbox)
                {
                    // send reward to inbox
                    var notification = CBSNotification.FromRewardDelivery(rewardDelivery, reward);
                    var sendNotificationResult = await NotificationModule.SendNotificationAsync(notification, entityID);
                    if (sendNotificationResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<GrantRewardResult>(sendNotificationResult.Error);
                    }
                }
                else
                {
                    var grantResult = await RewardModule.GrantRewardToProfileAsync(reward, entityID);
                    if (grantResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<GrantRewardResult>(grantResult.Error);
                    }
                    var rewardResult = grantResult.Result;
                    return new ExecuteResult<GrantRewardResult>
                    {
                        Result = rewardResult
                    };
                }
            }
            return new ExecuteResult<GrantRewardResult>();
        }
    }
}