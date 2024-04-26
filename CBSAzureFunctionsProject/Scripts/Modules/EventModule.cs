using PlayFab.Samples;
using PlayFab.AuthenticationModels;
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
using PlayFab;
using PlayFab.CloudScriptModels;
using PlayFab.AdminModels;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Azure.Storage.Queues;
using System.IO;
using System.Text;
using Medallion.Threading.Azure;
using Medallion.Threading;
using Azure.Storage.Queues.Models;

namespace CBS
{
    public class EventModule : BaseAzureModule
    {
        private static readonly FunctionExecuteBehavior EventExecuteBehavior = FunctionExecuteBehavior.AZURE_QUEUE;

        [FunctionName(AzureFunctions.StartCBSEventHandlerMethod)]
        public static async Task<dynamic> StartCBSEventHandlerTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionEventProccesRequest>();
            var eventID = request.EventID;

            var setResult = await SetEventProccesStateAsync(eventID, true);
            if (setResult.Error != null)
            {
                return ErrorHandler.ThrowError(setResult.Error).AsFunctionResult();
            }

            if (EventExecuteBehavior == FunctionExecuteBehavior.AZURE_FUNCTION)
            {
                var executeResult = await StartCBSEventAsync(eventID, starter);
                if (executeResult.Error != null)
                {
                    await SetEventProccesStateAsync(eventID, false);
                    return ErrorHandler.ThrowError(executeResult.Error).AsFunctionResult();
                }

                return executeResult.Result.AsFunctionResult();
            }
            else
            {
                await CreateQueueIfNotExistAsync(AzureQueues.StartEventQueueName);
                var startRequest = new FunctionStartEventRequest
                {
                    EventID = eventID
                };
                var executeResult = await ExecuteFunctionTriggerAsync(startRequest, AzureQueues.StartCBSEventQueue);
                if (executeResult.Error != null)
                {
                    await SetEventProccesStateAsync(eventID, false);
                    return ErrorHandler.ThrowError(executeResult.Error).AsFunctionResult();
                }
                return executeResult.Result.AsFunctionResult();
            }
        }

        [FunctionName(AzureFunctions.StopCBSEventHandlerMethod)]
        public static async Task<dynamic> StopCBSEventHandlerTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionEventProccesRequest>();
            var eventID = request.EventID;
            var manual = request.Manual;

            var setResult = await SetEventProccesStateAsync(eventID, true);
            if (setResult.Error != null)
            {
                return ErrorHandler.ThrowError(setResult.Error).AsFunctionResult();
            }
            
            if (EventExecuteBehavior == FunctionExecuteBehavior.AZURE_FUNCTION)
            {
                var executeResult = await StopCBSEventAsync(eventID, manual, starter);
                if (executeResult.Error != null)
                {
                    await SetEventProccesStateAsync(eventID, false);
                    return ErrorHandler.ThrowError(executeResult.Error).AsFunctionResult();
                }

                return executeResult.Result.AsFunctionResult();
            }
            else
            {
                await CreateQueueIfNotExistAsync(AzureQueues.StopEventQueueName);
                var stopRequest = new FunctionStopEventRequest
                {
                    EventID = eventID,
                    Manual = manual
                };
                var executeResult = await ExecuteFunctionTriggerAsync(stopRequest, AzureQueues.StopCBSEventQueue);
                if (executeResult.Error != null)
                {
                    await SetEventProccesStateAsync(eventID, false);
                    return ErrorHandler.ThrowError(executeResult.Error).AsFunctionResult();
                }
                return executeResult.Result.AsFunctionResult();
            }
        }

        [FunctionName(AzureFunctions.ExecuteCBSEventHandlerMethod)]
        public static async Task<dynamic> ExecuteCBSEventHandlerTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionEventProccesRequest>();
            var eventID = request.EventID;

            var setResult = await SetEventProccesStateAsync(eventID, true);
            if (setResult.Error != null)
            {
                return ErrorHandler.ThrowError(setResult.Error).AsFunctionResult();
            }

            if (EventExecuteBehavior == FunctionExecuteBehavior.AZURE_FUNCTION)
            {
                var executeResult = await ExecuteEventTasksAsync(eventID, starter);
                if (executeResult.Error != null)
                {
                    await SetEventProccesStateAsync(eventID, false);
                    return ErrorHandler.ThrowError(executeResult.Error).AsFunctionResult();
                }

                return executeResult.Result.AsFunctionResult();
            }
            else
            {
                await CreateQueueIfNotExistAsync(AzureQueues.ExecuteEventQueueName);
                var executeRequest = new FunctionExecuteEventRequest
                {
                    EventID = eventID
                };
                var executeResult = await ExecuteFunctionTriggerAsync(executeRequest, AzureQueues.ExecuteCBSEventQueue);
                if (executeResult.Error != null)
                {
                    await SetEventProccesStateAsync(eventID, false);
                    return ErrorHandler.ThrowError(executeResult.Error).AsFunctionResult();
                }

                return executeResult.Result.AsFunctionResult();
            } 
        }

        [FunctionName(AzureQueues.StartCBSEventQueue)]
        public static async Task StartCBSEventQueue([QueueTrigger(AzureQueues.StartEventQueueName, Connection = StorageConnectionKey)] string argsString, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(argsString);
            var args = context.FunctionArgument;
            var rawData = args == null ? JsonConvert.SerializeObject(new object{}) : JsonConvert.SerializeObject(args);
            var request = JsonConvert.DeserializeObject<FunctionStartEventRequest>(rawData) as FunctionStartEventRequest;

            var eventID = request.EventID;

            await StartCBSEventAsync(eventID, starter);
        }

        [FunctionName(AzureQueues.StopCBSEventQueue)]
        public static async Task StopCBSEventQueue([QueueTrigger(AzureQueues.StopEventQueueName, Connection = StorageConnectionKey)] string argsString, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(argsString);
            var args = context.FunctionArgument;
            var rawData = args == null ? JsonConvert.SerializeObject(new object{}) : JsonConvert.SerializeObject(args);
            var request = JsonConvert.DeserializeObject<FunctionStopEventRequest>(rawData) as FunctionStopEventRequest;

            var eventID = request.EventID;
            var manual = request.Manual;

            await StopCBSEventAsync(eventID, manual, starter);
        }

        [FunctionName(AzureQueues.ExecuteCBSEventQueue)]
        public static async Task ExecuteCBSEventQueue([QueueTrigger(AzureQueues.ExecuteEventQueueName, Connection = StorageConnectionKey)] string argsString, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(argsString);
            var args = context.FunctionArgument;
            var rawData = args == null ? JsonConvert.SerializeObject(new object{}) : JsonConvert.SerializeObject(args);
            var request = JsonConvert.DeserializeObject<FunctionExecuteEventRequest>(rawData) as FunctionExecuteEventRequest;

            var eventID = request.EventID;

            await ExecuteEventTasksAsync(eventID, starter);
        }

        [FunctionName(AzureFunctions.GetEventQueueContainerMethod)]
        public static async Task<dynamic> GetEventQueueContainerTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var getResult = await GetEventQueueContainerAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetCBSEventsMethod)]
        public static async Task<dynamic> GetCBSEventsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetCBSEventsRequest>();

            var getResult = await GetCBSEventsAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetEventBadgeMethod)]
        public static async Task<dynamic> GetEventBadgeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetCBSEventsRequest>();
            var category = request.ByCategory;

            var getResult = await GetEventsBadgeAsync(category);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetCBSEventByIDMethod)]
        public static async Task<dynamic> GetCBSEventByIDTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetCBSEventRequest>();
            var eventID = request.EventID;

            var getResult = await GetCBSEventByID(eventID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.RevokeCBSEventHandlerMethod)]
        public static async Task<dynamic> RevokeCBSEventHandlerTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionEventProccesRequest>();
            var eventID = request.EventID;

            var revokeResult = await RevokeCBSEventAsync(eventID, starter);
            if (revokeResult.Error != null)
            {
                return ErrorHandler.ThrowError(revokeResult.Error).AsFunctionResult();
            }

            return revokeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetCBSEventsLogListMethod)]
        public static async Task<dynamic> GetCBSEventsLogListTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var getResult = await TableEventsAssistant.GetEventLogsAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> StartCBSEventAsync(string eventID, IDurableOrchestrationClient starter)
        {
            var getTaskResult = await GetFabScheduledTaskAsync(eventID);
            if (getTaskResult.Error != null)
            {
                await ThrowEventExecutionError(eventID, eventID, getTaskResult.Error.Message);
                return ErrorHandler.ThrowError<FunctionEmptyResult>(getTaskResult.Error);
            }
            var task = getTaskResult.Result;

            var containerDataResult = await GetEventsContainerAsync();
            if (containerDataResult.Error != null)
            {
                await ThrowEventExecutionError(eventID, task.Name, containerDataResult.Error.Message);
                return ErrorHandler.ThrowError<FunctionEmptyResult>(containerDataResult.Error);
            }
            var container = containerDataResult.Result;
            var metaData = container.GetMetaData(eventID);

            var executeType = metaData.ExecuteType;
            var durationType = metaData.DurationType;

            if (executeType == EventExecuteType.BY_CRON_EXPRESSION)
            {
                var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
                await CreateLockContainerIfNotExistAsync();
                await using (await locker.AcquireLockAsync(TitleKeys.EventsDataKey.ToLower()))
                {
                    await using var handle = await locker.TryAcquireLockAsync(TitleKeys.EventsDataKey.ToLower());  
                    var adminAPI = await GetFabAdminAPIAsync();

                    var request = new UpdateTaskRequest
                    {
                        Identifier = new PlayFab.AdminModels.NameIdentifier
                        {
                            Id = task.TaskId
                        },
                        IsActive = true,
                        Name = task.Name,
                        Description = task.Description,
                        Schedule = task.Schedule,
                        Type = ScheduledTaskType.CloudScriptAzureFunctions,
                        Parameter = new CloudScriptTaskParameter
                        {
                            FunctionName = AzureFunctions.ExecuteCBSEventHandlerMethod,
                            Argument = new FunctionEventProccesRequest
                            {
                                EventID = task.TaskId
                            }
                        }
                    };

                    var updateResult = await adminAPI.UpdateTaskAsync(request);
                    if (updateResult.Error != null)
                    {
                        await ThrowEventExecutionError(eventID, task.Name, updateResult.Error.ErrorMessage);
                        return ErrorHandler.ThrowError<FunctionEmptyResult>(updateResult.Error);
                    }

                    metaData.InProccess = false;
                    container.ApplyMetaData(metaData);

                    var saveContainerResult = await SaveEventContainerAsync(container);
                    if (saveContainerResult.Error != null)
                    {
                        await ThrowEventExecutionError(eventID, task.Name, updateResult.Error.ErrorMessage);
                        return ErrorHandler.ThrowError<FunctionEmptyResult>(saveContainerResult.Error);
                    }
                }
                
            }
            else if (executeType == EventExecuteType.MANUAL)
            {
                var executeResult = await ExecuteEventTasksAsync(eventID, starter);
                if (executeResult.Error != null)
                {
                    await ThrowEventExecutionError(eventID, task.Name, executeResult.Error.Message);
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(executeResult.Error);
                }
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ExecuteEventTasksAsync(string eventID, IDurableOrchestrationClient starter)
        {
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(TitleKeys.EventsDataKey.ToLower()))
            {
                await using var handle = await locker.TryAcquireLockAsync(TitleKeys.EventsDataKey.ToLower());  
                var getTaskResult = await GetFabScheduledTaskAsync(eventID);
                if (getTaskResult.Error != null)
                {
                    await ThrowEventExecutionError(eventID, eventID, getTaskResult.Error.Message);
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(getTaskResult.Error);
                }
                var task = getTaskResult.Result;

                var containerDataResult = await GetEventsContainerAsync();
                if (containerDataResult.Error != null)
                {
                    await ThrowEventExecutionError(eventID, task.Name, containerDataResult.Error.Message);
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(containerDataResult.Error);
                }
                var container = containerDataResult.Result;
                var metaData = container.GetMetaData(eventID);

                var executeType = metaData.ExecuteType;
                var durationType = metaData.DurationType;

                if (durationType == EventDurationType.DURABLE)
                {
                    if (metaData.IsRunning == true)
                    {
                        await ThrowEventExecutionError(eventID, task.Name, "Task already running");
                        return ErrorHandler.EventAlreadyRunning<FunctionEmptyResult>();
                    }

                    metaData.IsRunning = true;

                    var eventSeconds = metaData.DurationInSeconds;

                    var durableRequest = new FunctionDurableTaskRequest
                    {
                        EventID = eventID,
                        Delay = eventSeconds,
                        FunctionName = AzureFunctions.StopCBSEventHandlerMethod,
                        FunctionRequest = new FunctionEventProccesRequest
                        {
                            EventID = task.TaskId
                        }
                    };

                    var instanceID = metaData.InstanceID;
                    var stopTaskResult = await DurableTaskExecuter.StopDurableTaskAsync(instanceID, eventID, starter);
                    if (stopTaskResult.Error != null)
                    {
                        await ThrowEventExecutionError(eventID, task.Name, stopTaskResult.Error.Message);
                        return ErrorHandler.ThrowError<FunctionEmptyResult>(stopTaskResult.Error);
                    }

                    var durableResult = await DurableTaskExecuter.StartDurableTaskAsync(durableRequest, starter);
                    if (durableResult.Error != null)
                    {
                        await ThrowEventExecutionError(eventID, task.Name, durableResult.Error.Message);
                        return ErrorHandler.ThrowError<FunctionEmptyResult>(durableResult.Error);
                    }
                    instanceID = durableResult.Result.DurableTaskInstanceID;
                    var endDate = durableResult.Result.ExecuteDate;

                    metaData.InstanceID = instanceID;
                    metaData.StartDate = ServerTimeUTC;
                    metaData.EndDate = endDate;
                    metaData.InProccess = false;

                    container.ApplyMetaData(metaData);

                    var saveMetaResult = await SaveEventContainerAsync(container);
                    if (saveMetaResult.Error != null)
                    {
                        await ThrowEventExecutionError(eventID, task.Name, saveMetaResult.Error.Message);
                        return ErrorHandler.ThrowError<FunctionEmptyResult>(saveMetaResult.Error);
                    }
                }
                else
                {
                    metaData.InProccess = false;
                    container.ApplyMetaData(metaData);

                    var saveMetaResult = await SaveEventContainerAsync(container);
                    if (saveMetaResult.Error != null)
                    {
                        await ThrowEventExecutionError(eventID, task.Name, saveMetaResult.Error.Message);
                        return ErrorHandler.ThrowError<FunctionEmptyResult>(saveMetaResult.Error);
                    }
                }

                var startTasks = metaData.StartTasks;
                ExecuteTaskEventContainer(startTasks, starter);

                var sendLogResult = await SendSuccessStartLogAsync(durationType == EventDurationType.DURABLE, eventID, task.Name);
                if (sendLogResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(sendLogResult.Error);
                }

                return new ExecuteResult<FunctionEmptyResult>
                {
                    Result = new FunctionEmptyResult()
                };
            }
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> StopCBSEventAsync(string eventID, bool manual, IDurableOrchestrationClient starter)
        {
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(TitleKeys.EventsDataKey.ToLower()))
            {
                await using var handle = await locker.TryAcquireLockAsync(TitleKeys.EventsDataKey.ToLower());   
                var getTaskResult = await GetFabScheduledTaskAsync(eventID);
                if (getTaskResult.Error != null)
                {
                    await ThrowEventExecutionError(eventID, eventID, getTaskResult.Error.Message);
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(getTaskResult.Error);
                }
                var task = getTaskResult.Result;

                var containerDataResult = await GetEventsContainerAsync();
                if (containerDataResult.Error != null)
                {
                    await ThrowEventExecutionError(eventID, task.Name, getTaskResult.Error.Message);
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(containerDataResult.Error);
                }
                var container = containerDataResult.Result;
                var metaData = container.GetMetaData(eventID);

                var executeType = metaData.ExecuteType;
                var durationType = metaData.DurationType;

                if (executeType == EventExecuteType.BY_CRON_EXPRESSION)
                {
                    var adminAPI = await GetFabAdminAPIAsync();

                    var request = new UpdateTaskRequest
                    {
                        Identifier = new PlayFab.AdminModels.NameIdentifier
                        {
                            Id = task.TaskId
                        },
                        IsActive = manual == true ? false : true,
                        Name = task.Name,
                        Description = task.Description,
                        Schedule = task.Schedule,
                        Type = ScheduledTaskType.CloudScriptAzureFunctions,
                        Parameter = new CloudScriptTaskParameter
                        {
                            FunctionName = AzureFunctions.ExecuteCBSEventHandlerMethod,
                            Argument = new FunctionEventProccesRequest
                            {
                                EventID = task.TaskId
                            }
                        }
                    };

                    var updateResult = await adminAPI.UpdateTaskAsync(request);
                    if (updateResult.Error != null)
                    {
                        await ThrowEventExecutionError(eventID, task.Name, updateResult.Error.ErrorMessage);
                        return ErrorHandler.ThrowError<FunctionEmptyResult>(updateResult.Error);
                    }

                    metaData.InProccess = false;
                    container.ApplyMetaData(metaData);

                    var saveContainerResult = await SaveEventContainerAsync(container);
                    if (saveContainerResult.Error != null)
                    {
                        await ThrowEventExecutionError(eventID, task.Name, saveContainerResult.Error.Message);
                        return ErrorHandler.ThrowError<FunctionEmptyResult>(saveContainerResult.Error);
                    }
                }

                if (durationType == EventDurationType.DURABLE && metaData.IsRunning)
                {
                    var instanceID = metaData.InstanceID;
                    var stopTaskResult = await DurableTaskExecuter.StopDurableTaskAsync(instanceID, eventID, starter);
                    if (stopTaskResult.Error != null)
                    {
                        //await ThrowEventExecutionError(eventID, task.Name, stopTaskResult.Error.Message);
                        //return ErrorHandler.ThrowError<FunctionEmptyResult>(stopTaskResult.Error);
                    }
                }

                metaData.InstanceID = null;
                metaData.IsRunning = false;
                metaData.StartDate = null;
                metaData.EndDate = null;
                metaData.InProccess = false;

                var endTasks = metaData.EndTasks;

                container.ApplyMetaData(metaData);
                var saveResult = await SaveEventContainerAsync(container);
                if (saveResult.Error != null)
                {
                    await ThrowEventExecutionError(eventID, task.Name, saveResult.Error.Message);
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
                }

                ExecuteTaskEventContainer(endTasks, starter);

                var sendLogResult = await SendSuccessStopLogAsync(eventID, task.Name);
                if (sendLogResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(sendLogResult.Error);
                }

                return new ExecuteResult<FunctionEmptyResult>
                {
                    Result = new FunctionEmptyResult()
                };
            }
            
        }

        public static async Task<ExecuteResult<EventsData>> GetEventsContainerAsync()
        {
            var dataResult = await GetInternalTitleDataAsObjectAsync<EventsData>(TitleKeys.EventsDataKey);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<EventsData>(dataResult.Error);
            }
            return new ExecuteResult<EventsData>
            {
                Result = dataResult.Result ?? new EventsData()
            };
        }

        public static async Task<ExecuteResult<EventMetaData>> GetTaskMetaDataAsync(string eventID)
        {
            var containerResult = await GetEventsContainerAsync();
            if (containerResult.Error != null)
            {
                return ErrorHandler.ThrowError<EventMetaData>(containerResult.Error);
            }
            var container = containerResult.Result;
            var metaData = container.GetMetaData(eventID);

            return new ExecuteResult<EventMetaData>
            {
                Result = metaData
            };
        }

        public static async Task<ExecuteResult<ScheduledTask>> GetFabScheduledTaskAsync(string eventID)
        {
            var adminAPI = await GetFabAdminAPIAsync();
            var taskRequest = new GetTasksRequest
            {
                Identifier = new PlayFab.AdminModels.NameIdentifier
                {
                    Id = eventID
                }
            };
            var tasksResult = await adminAPI.GetTasksAsync(taskRequest);
            if (tasksResult.Error != null)
            {
                return ErrorHandler.ThrowError<ScheduledTask>(tasksResult.Error);
            }
            var tasks = tasksResult.Result.Tasks;
            var task = tasks.FirstOrDefault();
            if (task == null)
            {
                return ErrorHandler.EventNotFound<ScheduledTask>();
            }

            return new ExecuteResult<ScheduledTask>
            {
                Result = task
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SaveEventContainerAsync(EventsData container)
        {
            var rawData = JsonPlugin.ToJsonCompress(container);
            var saveResult = await SaveInternalTitleDataAsync(TitleKeys.EventsDataKey, rawData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionGetEventQueueResult>> GetEventQueueContainerAsync()
        {
            var executeResult = await GetEventListInQueueNamesAsync(AzureQueues.ExecuteEventQueueName);
            if (executeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetEventQueueResult>(executeResult.Error);
            }
            var executeList = executeResult.Result;

            var startResult = await GetEventListInQueueNamesAsync(AzureQueues.StartEventQueueName);
            if (startResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetEventQueueResult>(startResult.Error);
            }
            var startList = startResult.Result;

            var stopResult = await GetEventListInQueueNamesAsync(AzureQueues.StopEventQueueName);
            if (stopResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetEventQueueResult>(stopResult.Error);
            }
            var stopList = stopResult.Result;

            return new ExecuteResult<FunctionGetEventQueueResult>
            {
                Result = new FunctionGetEventQueueResult
                {
                    QueueContainer = new EventQueueContainer
                    {
                        ExecuteMessages = executeList,
                        StartMessages = startList,
                        StopMessages = stopList
                    }
                }
            };
        }

        public static async Task<ExecuteResult<List<string>>> GetEventListInQueueNamesAsync(string queueName)
        {
            var options = new QueueClientOptions 
            {
                MessageEncoding = QueueMessageEncoding.None
            };
            var queueClient = new QueueClient(StorageConnectionString, queueName);
            
            try
            {
                var messageResult = await queueClient.ReceiveMessagesAsync();
                var messages = messageResult.Value;
                var eventList = new List<string>();
                foreach (var message in messages)
                {
                    StreamReader reader = new StreamReader(message.Body.ToStream());
                    var rawResponse = Encoding.UTF8.GetString(Convert.FromBase64String(reader.ReadToEnd()));
                    var body = JsonPlugin.FromJson<FunctionExecutionContext<dynamic>>(rawResponse);
                    var args = body.FunctionArgument;
                    var rawData = args == null ? JsonConvert.SerializeObject(new object{}) : JsonConvert.SerializeObject(args);
                    var request = JsonConvert.DeserializeObject<dynamic>(rawData);
                    var eventID = (string)request.EventID;
                    eventList.Add(eventID);
                }
                return new ExecuteResult<List<string>>
                {
                    Result = eventList
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<List<string>>(err);
            }
        }

        public static async Task<ExecuteResult<Dictionary<string, QueueMessage>>> GetEventListInQueueIDsAsync(string queueName)
        {
            var queueClient = new QueueClient(StorageConnectionString, queueName);
            
            try
            {
                var messageResult = await queueClient.ReceiveMessagesAsync();
                var messages = messageResult.Value;
                var eventList = new Dictionary<string, QueueMessage>();
                foreach (var message in messages)
                {
                    StreamReader reader = new StreamReader(message.Body.ToStream());
                    var rawResponse = Encoding.UTF8.GetString(Convert.FromBase64String(reader.ReadToEnd()));
                    var body = JsonPlugin.FromJson<FunctionExecutionContext<dynamic>>(rawResponse);
                    var args = body.FunctionArgument;
                    var rawData = args == null ? JsonConvert.SerializeObject(new object{}) : JsonConvert.SerializeObject(args);
                    var request = JsonConvert.DeserializeObject<dynamic>(rawData);
                    var eventID = (string)request.EventID;
                    eventList[eventID] = message;
                }
                return new ExecuteResult<Dictionary<string, QueueMessage>>
                {
                    Result = eventList
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<List<string>>(err);
            }
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> DeleteEventFromQueueAsync(string eventID)
        {
            var queueNameToDelete = string.Empty;
            QueueMessage queueMessageToDelete = null;
            var executeResult = await GetEventListInQueueIDsAsync(AzureQueues.ExecuteEventQueueName);
            if (executeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(executeResult.Error);
            }
            var executeList = executeResult.Result ?? new Dictionary<string, QueueMessage>();
            if (executeList.ContainsKey(eventID))
            {
                queueNameToDelete = AzureQueues.ExecuteEventQueueName;
                queueMessageToDelete = executeList[eventID];
            }

            var startResult = await GetEventListInQueueIDsAsync(AzureQueues.StartEventQueueName);
            if (startResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(startResult.Error);
            }
            var startList = startResult.Result ?? new Dictionary<string, QueueMessage>();
            if (startList.ContainsKey(eventID))
            {
                queueNameToDelete = AzureQueues.StartEventQueueName;
                queueMessageToDelete = startList[eventID];
            }

            var stopResult = await GetEventListInQueueIDsAsync(AzureQueues.StopEventQueueName);
            if (stopResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(stopResult.Error);
            }
            var stopList = stopResult.Result ?? new Dictionary<string, QueueMessage>();
            if (stopList.ContainsKey(eventID))
            {
                queueNameToDelete = AzureQueues.StopEventQueueName;
                queueMessageToDelete = stopList[eventID];
            }

            if (!string.IsNullOrEmpty(queueNameToDelete) && queueMessageToDelete != null)
            {
                var queueClient = new QueueClient(StorageConnectionString, queueNameToDelete);
                var deleteResult = await queueClient.DeleteMessageAsync(queueMessageToDelete.MessageId, queueMessageToDelete.PopReceipt);
                if (deleteResult.IsError)
                {
                    return ErrorHandler.InvalidInput<FunctionEmptyResult>();
                }
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionGetCBSEventsResult>> GetCBSEventsAsync(FunctionGetCBSEventsRequest request)
        {
            var activeOnly = request.ActiveOnly;
            var byCategory = request.ByCategory;

            var adminAPI = await GetFabAdminAPIAsync();
            var tasksRequest = new GetTasksRequest();
            var getTasksResult = await adminAPI.GetTasksAsync(tasksRequest);
            if (getTasksResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetCBSEventsResult>(getTasksResult.Error);
            }
            var tasks = getTasksResult.Result.Tasks ?? new List<ScheduledTask>();

            var getContainerResult = await GetEventsContainerAsync();
            if (getContainerResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetCBSEventsResult>(getContainerResult.Error);
            }
            var container = getContainerResult.Result;

            var cbsEvents = new List<CBSEvent>();
            foreach (var task in tasks)
            {
                var eventID = task.TaskId;
                var metaData = container.GetMetaData(eventID);
                var cbsEvent = new CBSEvent
                {
                    ID = eventID,
                    InstanceID = metaData.InstanceID,
                    DisplayName = task.Name,
                    Description = task.Description,
                    CronExpression = task.Schedule,
                    Category = metaData.Category,
                    IsRunning = metaData.IsRunning,
                    LastRunTime = task.LastRunTime,
                    NextRunTime = task.NextRunTime,
                    StartDate = metaData.StartDate,
                    EndDate = metaData.EndDate,
                    CustomDataClassName = metaData.CustomDataClassName,
                    CustomRawData = metaData.CustomRawData
                };
                cbsEvents.Add(cbsEvent);
            }

            if (activeOnly)
                cbsEvents = cbsEvents.Where(x=>x.IsRunning).ToList();
            if (!string.IsNullOrEmpty(byCategory))
                cbsEvents = cbsEvents.Where(x=>x.Category == byCategory).ToList();

            return new ExecuteResult<FunctionGetCBSEventsResult>
            {
                Result = new FunctionGetCBSEventsResult
                {
                    Events = cbsEvents
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetCBSEventResult>> GetCBSEventByID(string eventID)
        {
            var adminAPI = await GetFabAdminAPIAsync();
            var tasksRequest = new GetTasksRequest
            {
                Identifier = new PlayFab.AdminModels.NameIdentifier
                { 
                    Id = eventID
                }
            };
            var taskResult = await adminAPI.GetTasksAsync(tasksRequest);
            if (taskResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetCBSEventResult>(taskResult.Error);
            }
            var tasks = taskResult.Result.Tasks ?? new List<ScheduledTask>();
            var task = tasks.FirstOrDefault();
            if (task == null)
            {
                return ErrorHandler.TaskIDNotFound<FunctionGetCBSEventResult>();
            }

            var metaDataResult = await GetTaskMetaDataAsync(eventID);
            if (metaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetCBSEventResult>(metaDataResult.Error);
            }
            var metaData = metaDataResult.Result;

            var cbsEvent = new CBSEvent
            {
                ID = eventID,
                InstanceID = metaData.InstanceID,
                DisplayName = task.Name,
                Description = task.Description,
                CronExpression = task.Schedule,
                Category = metaData.Category,
                IsRunning = metaData.IsRunning,
                LastRunTime = task.LastRunTime,
                NextRunTime = task.NextRunTime,
                StartDate = metaData.StartDate,
                EndDate = metaData.EndDate
            };

            return new ExecuteResult<FunctionGetCBSEventResult>
            {
                Result = new FunctionGetCBSEventResult
                {
                    Event = cbsEvent
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SetEventProccesStateAsync(string eventID, bool state)
        {
            var lockID = TitleKeys.EventsDataKey;
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(lockID))
            {
                await using var handle = await locker.TryAcquireLockAsync(lockID);   
                var containerDataResult = await GetEventsContainerAsync();
                if (containerDataResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(containerDataResult.Error);
                }
                var container = containerDataResult.Result;
                var metaData = container.GetMetaData(eventID);
                metaData.InProccess = state;
                container.ApplyMetaData(metaData);

                var saveResult = await SaveEventContainerAsync(container);
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

        public static async Task<ExecuteResult<FunctionEmptyResult>> RevokeCBSEventAsync(string eventID, IDurableOrchestrationClient starter)
        {
            var getTaskResult = await GetFabScheduledTaskAsync(eventID);
            if (getTaskResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(getTaskResult.Error);
            }
            var task = getTaskResult.Result;

            var containerDataResult = await GetEventsContainerAsync();
            if (containerDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(containerDataResult.Error);
            }
            var container = containerDataResult.Result;
            var metaData = container.GetMetaData(eventID);

            if (metaData.DurationType == EventDurationType.DURABLE)
            {
                var instanceID = metaData.InstanceID;
                if (!string.IsNullOrEmpty(instanceID))
                {
                    try
                    {
                        await DurableTaskExecuter.StopDurableTaskAsync(instanceID, eventID, starter);
                    }
                    catch{}
                }
            }

            metaData.IsRunning = false;
            metaData.InProccess = false;
            metaData.InstanceID = null;
            metaData.StartDate = null;
            metaData.EndDate = null;

            container.ApplyMetaData(metaData);

            var deleteQueueResult = await DeleteEventFromQueueAsync(eventID);
            if (deleteQueueResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(deleteQueueResult.Error);
            }

            var saveResult = await SaveEventContainerAsync(container);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };

        }

        public static async Task<ExecuteResult<FunctionBadgeResult>> GetEventsBadgeAsync(string category)
        {
            var getEventsResult = await GetCBSEventsAsync(new FunctionGetCBSEventsRequest
            {
                ActiveOnly = true,
                ByCategory = category
            });
            if (getEventsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBadgeResult>(getEventsResult.Error);
            }
            var events = getEventsResult.Result.Events ?? new List<CBSEvent>();
            var eventsCount = events.Count;

            return new ExecuteResult<FunctionBadgeResult>
            {
                Result = new FunctionBadgeResult
                {
                    Count = eventsCount
                }                
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ThrowEventExecutionError(string eventID, string eventName, string errorMessage)
        {
            var setResult = await SetEventProccesStateAsync(eventID, false);
            if (setResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(setResult.Error);
            }

            var message = string.Format("Event {0} executed with error. {1}", eventName, errorMessage);
            var logMessage = new EventExecutionLog
            {
                EventID = eventID,
                EventName = eventName,
                Message = message,
                IsSuccess = false,
                LogDate = ServerTimeUTC
            };

            var sendResult = await TableEventsAssistant.SendLogAsync(logMessage);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(sendResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SendSuccessStartLogAsync(bool isDurable, string eventID, string eventName)
        {
            var messateTitle = isDurable == true ? "started" : "executed";
            var message = string.Format("Event {0} successfully {1}", eventName, messateTitle);
            var logMessage = new EventExecutionLog
            {
                EventID = eventID,
                EventName = eventName,
                Message = message,
                IsSuccess = true,
                LogDate = ServerTimeUTC
            };

            var sendResult = await TableEventsAssistant.SendLogAsync(logMessage);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(sendResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SendSuccessStopLogAsync(string eventID, string eventName)
        {
            var message = string.Format("Event {0} successfully stopped", eventName);
            var logMessage = new EventExecutionLog
            {
                EventID = eventID,
                EventName = eventName,
                Message = message,
                IsSuccess = true,
                LogDate = ServerTimeUTC
            };

            var sendResult = await TableEventsAssistant.SendLogAsync(logMessage);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(sendResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static void ExecuteTaskEventContainer(TaskEventContainer container, IDurableOrchestrationClient starter)
        {
            if (container == null)
                return;
            Task.Run(async ()=>
            {
                await ExecuteTasksEventContainerAsync(container, starter);
            });
        }

        public static void ExecuteProfileEventContainer(string profileID, ProfileEventContainer container)
        {
            if (container == null)
                return;
            Task.Run(async ()=>
            {
                await ExecuteProfileEventContainerAsync(profileID, container);
            });
        }

        public static void ExecuteClanEventContainer(string clanID, ClanEventContainer container)
        {
            if (container == null)
                return;
            Task.Run(async ()=>
            {
                await ExecuteClanEventContainerAsync(clanID, container);
            });
        }

        private static async Task<ExecuteResult<dynamic>> ExecuteTasksEventContainerAsync(TaskEventContainer container, IDurableOrchestrationClient starter)
        {
            var isValid = !container.IsEmpty();
            if (isValid)
            {
                var events = container.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    var eventBody = events[i];
                    var eventType = eventBody.EventType;
                    switch (eventType)
                    {
                        case TaskEventType.RESET_PROFILE_LEADERBOARD:
                            var eventLeaderboard = eventBody.GetContent<TaskResetLeaderboardEvent>();
                            var statisticName = eventLeaderboard.StatisticName;
                            await StatisticModule.ResetStatisticsAsync(statisticName);
                            break;
                        case TaskEventType.UPDATE_PROFILE_EXP_MULTIPLY:
                            var eventExpMultiply = eventBody.GetContent<TaskSetProfileExpMultiplyEvent>();
                            var expMultiply = eventExpMultiply.ExpMultiply;
                            await ProfileExpModule.UpdateExpMultiplierAsync(expMultiply);
                            break;
                        case TaskEventType.ENANLE_OR_DISABLE_STORE:
                            var eventDisableStore = eventBody.GetContent<TaskSetStoreActivityEvent>();
                            var storeID = eventDisableStore.StoreID;
                            var storeActivity = eventDisableStore.Enabled;
                            await StoreModule.SetStoreActivityAsync(storeID, storeActivity);
                            break;
                        case TaskEventType.SET_STORE_ITEM_PRICE:
                            var eventItemPrice = eventBody.GetContent<TaskSetStoreItemPriceEvent>();
                            var itemStoreID = eventItemPrice.StoreID;
                            var itemIDToUpdate = eventItemPrice.ItemID;
                            var currencyCode = eventItemPrice.CurrencyCode;
                            var currencyValue = eventItemPrice.CurrencyValue;
                            await StoreModule.SetStoreItemPriceAsync(itemStoreID, itemIDToUpdate, currencyCode, currencyValue);
                            break;
                        case TaskEventType.ENABLE_OR_DISABLE_ITEM_IN_STORE:
                            var eventItemActivity = eventBody.GetContent<TaskSetItemStoreActivityEvent>();
                            var itemActivityStoreID = eventItemActivity.StoreID;
                            var itemIDToEnabled = eventItemActivity.ItemID;
                            var itemActivity = eventItemActivity.Enabled;
                            await StoreModule.SetStoreItemActivityAsync(itemActivityStoreID, itemIDToEnabled, itemActivity);
                            break;
                        case TaskEventType.START_STORE_GLOBAL_SPECIAL_OFFER:
                            var eventStartSpecialOffer = eventBody.GetContent<TaskStartSpecialOfferEvent>();
                            var itemStartID = eventStartSpecialOffer.ItemID;
                            await SpecialOfferModule.StartSpecialOffer(itemStartID, starter);
                            break;
                        case TaskEventType.STOP_STORE_GLOBAL_SPECIAL_OFFER:
                            var eventStopSpecialOffer = eventBody.GetContent<TaskStopSpecialOfferEvent>();
                            var itemStopID = eventStopSpecialOffer.ItemID;
                            await SpecialOfferModule.StopSpecialOffer(itemStopID, starter);
                            break;
                        case TaskEventType.SEND_MESSAGE_TO_CHAT:
                            var eventChatMessage = eventBody.GetContent<TaskSendMessageToChatEvent>();
                            var chatID = eventChatMessage.ChatID;
                            var chatMessage = eventChatMessage.ChatMessage;
                            await ChatModule.SendSystemMessageToChatAsync(chatID, chatMessage);
                            break;
                        case TaskEventType.ENABLE_OR_DISABLE_CALENDAR:
                            var eventCalendarActivity = eventBody.GetContent<TaskSetCalendarActivityEvent>();
                            var calendarID = eventCalendarActivity.CalendarID;
                            var calendarActivity = eventCalendarActivity.Enabled;
                            await CalendarModule.SetCalendarActivityAsync(calendarID, calendarActivity);
                            break;
                        case TaskEventType.START_BATTLE_PASS:
                            var eventStartBattlePass = eventBody.GetContent<TaskStartBattlePassEvent>();
                            var startPassID = eventStartBattlePass.BattlePassID;
                            await BattlePassModule.StartBattlePassInstance(startPassID, starter);
                            break;
                        case TaskEventType.STOP_BATTLE_PASS:
                            var eventStopBattlePass = eventBody.GetContent<TaskStopBattlePassEvent>();
                            var stopPassID = eventStopBattlePass.BattlePassID;
                            await BattlePassModule.StopBattlePassInstance(stopPassID, starter);
                            break;
                        case TaskEventType.SEND_NOTIFICATION:
                            var eventNotification = eventBody.GetContent<TaskSendNotificationEvent>();
                            var notificationID = eventNotification.NotificationID;
                            await NotificationModule.SendNotificationAsync(notificationID, null);
                            break;
                        case TaskEventType.REWARD_MEMBERS_OF_TOP_CLAN:
                            var topClanReward = eventBody.GetContent<TaskRewardMembersOfTopClansEvent>();
                            var reward = topClanReward.Reward;
                            var clanStatisticName = topClanReward.StatisticName;
                            var nTop = topClanReward.nTop;
                            await RewardModule.RewardAllMembersOfTopClansAsync(clanStatisticName, nTop, reward);
                            break;
                        case TaskEventType.EXECUTE_EVENTS_FOR_MEMBERS_OF_TOP_CLAN:
                            var topClanEvents = eventBody.GetContent<TaskExecuteEventMembersOfTopClansEvent>();
                            var eventsContainer = topClanEvents.Events;
                            var clanStatisticNameForEvents = topClanEvents.StatisticName;
                            var nTopForEvents = topClanEvents.nTop;
                            await ExecuteEventsForAllMembersOfTopClansAsync(clanStatisticNameForEvents, nTopForEvents, eventsContainer);
                            break;
                        case TaskEventType.UPDATE_TITLE_DATA:
                            var eventTitleData = eventBody.GetContent<TaskUpdateTitleDataEvent>();
                            var lastSavedData = eventTitleData.RawDataToUpdate;
                            var dataKey = eventTitleData.TitleID;
                            var compressedTitleData = JsonPlugin.ToJsonCompress(JsonPlugin.FromJson<dynamic>(lastSavedData));
                            await SavePublicTitleDataAsync(dataKey, compressedTitleData);
                            break;
                        case TaskEventType.EXECUTE_FUNCTION:
                            var eventFunction = eventBody.GetContent<TaskExecuteFunctionEvent>();
                            var functionName = eventFunction.FunctionName;
                            var requestRaw = eventFunction.RequestRaw;
                            var functionRequest = JsonPlugin.FromJson<dynamic>(requestRaw);

                            PlayFabSettings.staticSettings.TitleId = TitleId;
                            PlayFabSettings.staticSettings.DeveloperSecretKey = SercetKey;
                            var entityTokenResult = await PlayFabAuthenticationAPI.GetEntityTokenAsync(new GetEntityTokenRequest());
                            var entityToken = entityTokenResult.Result.EntityToken;
                            var entity = entityTokenResult.Result.Entity;

                            var executeRequest = new ExecuteFunctionRequest
                            {
                                FunctionName = functionName,
                                FunctionParameter = functionRequest,
                                Entity = new PlayFab.CloudScriptModels.EntityKey
                                {
                                    Id = entity.Id,
                                    Type = entity.Type
                                }
                            };
                            await PlayFabCloudScriptAPI.ExecuteFunctionAsync(executeRequest);
                            break;
                        default:
                            break;
                    }
                }
            }
            return null;
        }

        private static async Task<ExecuteResult<dynamic>> ExecuteProfileEventContainerAsync(string profileID, ProfileEventContainer container)
        {
            var isValid = !container.IsEmpty();
            if (isValid)
            {
                var events = container.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    var eventBody = events[i];
                    var eventType = eventBody.EventType;
                    switch (eventType)
                    {
                        case ProfileEventType.ADD_STATISTIC_VALUE:
                            var eventAdd = eventBody.GetContent<ProfileAddStatisticEvent>();
                            var staticiticName = eventAdd.StatisticName;
                            var statisitcValue = eventAdd.StatisticValue;
                            await StatisticModule.AddProfileStatisticValueAsync(profileID, staticiticName, statisitcValue);
                            break;
                        case ProfileEventType.CONVERT_STATISTIC_TO_EXP:
                            var eventConvert = eventBody.GetContent<ProfileConvertStatisticToExpEvent>();
                            var staticiticNameToConvert = eventConvert.StatisticName;
                            await ProfileExpModule.AddExpFromStatisitcValueAsync(profileID, staticiticNameToConvert);
                            break;
                        case ProfileEventType.ADD_ACHIEVEMENT_POINT:
                            var eventAchievement = eventBody.GetContent<ProfileAddAchievementPointEvent>();
                            var achievementID = eventAchievement.AchievementID;
                            var achievementValue = eventAchievement.Points;
                            await AchievementsModule.ModifyAchievementsPointsAsync(profileID, achievementID, ModifyMethod.ADD, achievementValue);
                            break;
                        case ProfileEventType.UPDATE_STATISTIC_VALUE:
                            var eventUpdate = eventBody.GetContent<ProfileUpdateStatisticEvent>();
                            staticiticName = eventUpdate.StatisticName;
                            statisitcValue = eventUpdate.StatisticValue;
                            await StatisticModule.UpdateProfileStatisticValueAsync(profileID, staticiticName, statisitcValue);
                            break;
                        case ProfileEventType.SET_CUSTOM_DATA:
                            var eventSetData = eventBody.GetContent<ProfileSetCustomDataEvent>();
                            var dataKey = eventSetData.DataKey;
                            var dataValue = eventSetData.DataValue;
                            await ProfileModule.SetProfileDataAsync(profileID, dataKey, dataValue);
                            break;
                        case ProfileEventType.GRANT_SPECIAL_OFFER:
                            var eventGrantOffer = eventBody.GetContent<ProfileGrantSpefialOfferEvent>();
                            var itemID = eventGrantOffer.SpecialOfferItemID;
                            await SpecialOfferModule.GrantSpecialOfferToProfileAsync(profileID, itemID);
                            break;
                        case ProfileEventType.GRANT_CALENDAR_INSTANCE:
                            var eventGrantCalendar = eventBody.GetContent<ProfileGrantCalendarEvent>();
                            var calendarID = eventGrantCalendar.CalendarID;
                            await CalendarModule.GrantCalendarInstanceToProfileAsync(profileID, calendarID);
                            break;
                        case ProfileEventType.GRANT_AVATAR:
                            var eventGrantAvatar = eventBody.GetContent<ProfileGrantAvatarEvent>();
                            var avatarID = eventGrantAvatar.AvatarID;
                            await ProfileModule.GrantAvatarAsync(profileID, avatarID);
                            break;
                        case ProfileEventType.SEND_NOTIFICATION:
                            var eventNotification = eventBody.GetContent<ProfileSendNotificationEvent>();
                            var notificationID = eventNotification.NotificationID;
                            await NotificationModule.SendNotificationAsync(notificationID, profileID);
                            break;
                        case ProfileEventType.EXECUTE_FUNCTION:
                            var eventFunction = eventBody.GetContent<ProfileExecuteFunctionEvent>();
                            var functionName = eventFunction.FunctionName;
                            var requestRaw = eventFunction.RequestRaw;
                            var functionRequest = JsonPlugin.FromJson<dynamic>(requestRaw);
                            functionRequest[PlayfabHelper.ProfileIDArgsKey] = profileID;

                            PlayFabSettings.staticSettings.TitleId = TitleId;
                            PlayFabSettings.staticSettings.DeveloperSecretKey = SercetKey;
                            var entityTokenResult = await PlayFabAuthenticationAPI.GetEntityTokenAsync(new GetEntityTokenRequest());
                            var entityToken = entityTokenResult.Result.EntityToken;
                            var entity = entityTokenResult.Result.Entity;

                            var executeRequest = new ExecuteFunctionRequest
                            {
                                FunctionName = functionName,
                                FunctionParameter = functionRequest,
                                Entity = new PlayFab.CloudScriptModels.EntityKey
                                {
                                    Id = entity.Id,
                                    Type = entity.Type
                                }
                            };
                            await PlayFabCloudScriptAPI.ExecuteFunctionAsync(executeRequest);
                            break;
                        default:
                            break;
                    }
                }
            }
            return null;
        }

        private static async Task<ExecuteResult<dynamic>> ExecuteClanEventContainerAsync(string clanID, ClanEventContainer container)
        {
            var isValid = !container.IsEmpty();
            if (isValid)
            {
                var events = container.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    var eventBody = events[i];
                    var eventType = eventBody.EventType;
                    switch (eventType)
                    {
                        case ClanEventType.ADD_STATISTIC_VALUE:
                            var eventAdd = eventBody.GetContent<ClanAddStatisticEvent>();
                            var staticiticName = eventAdd.StatisticName;
                            var statisitcValue = eventAdd.StatisticValue;
                            await ClanStatisticModule.AddClanStatisticValueAsync(clanID, staticiticName, statisitcValue);
                            break;
                        case ClanEventType.UPDATE_STATISTIC_VALUE:
                            var eventUpdate = eventBody.GetContent<ClanUpdateStatisticEvent>();
                            staticiticName = eventUpdate.StatisticName;
                            statisitcValue = eventUpdate.StatisticValue;
                            await ClanStatisticModule.UpdateClanStatisticValueAsync(clanID, staticiticName, statisitcValue);
                            break;
                        case ClanEventType.SET_CUSTOM_DATA:
                            var eventSetData = eventBody.GetContent<ClanSetCustomDataEvent>();
                            var dataKey = eventSetData.DataKey;
                            var dataValue = eventSetData.DataValue;
                            var dataDict = new Dictionary<string, string>();
                            dataDict[dataKey] = dataValue;
                            await ClanModule.SetClanCustomDataAsync(clanID, dataDict);
                            break;
                        case ClanEventType.EXECUTE_FUNCTION:
                            var eventFunction = eventBody.GetContent<ClanExecuteFunctionEvent>();
                            var functionName = eventFunction.FunctionName;
                            var requestRaw = eventFunction.RequestRaw;
                            var functionRequest = JsonPlugin.FromJson<dynamic>(requestRaw);
                            functionRequest[PlayfabHelper.ClanIDArgsKey] = clanID;

                            PlayFabSettings.staticSettings.TitleId = TitleId;
                            PlayFabSettings.staticSettings.DeveloperSecretKey = SercetKey;
                            var entityTokenResult = await PlayFabAuthenticationAPI.GetEntityTokenAsync(new GetEntityTokenRequest());
                            var entityToken = entityTokenResult.Result.EntityToken;
                            var entity = entityTokenResult.Result.Entity;

                            var executeRequest = new ExecuteFunctionRequest
                            {
                                FunctionName = functionName,
                                FunctionParameter = functionRequest,
                                Entity = new PlayFab.CloudScriptModels.EntityKey
                                {
                                    Id = entity.Id,
                                    Type = entity.Type
                                }
                            };
                            await PlayFabCloudScriptAPI.ExecuteFunctionAsync(executeRequest);
                            break;
                        default:
                            break;
                    }
                }
            }
            return null;
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ExecuteFunctionTriggerAsync(FunctionBaseRequest request, string functionName)
        {
            PlayFabSettings.staticSettings.TitleId = TitleId;
            PlayFabSettings.staticSettings.DeveloperSecretKey = SercetKey;
            var entityTokenResult = await PlayFabAuthenticationAPI.GetEntityTokenAsync(new GetEntityTokenRequest());
            var entityToken = entityTokenResult.Result.EntityToken;
            var entity = entityTokenResult.Result.Entity;

            var executeRequest = new ExecuteFunctionRequest
            {
                FunctionName = functionName,
                FunctionParameter = request,
                Entity = new PlayFab.CloudScriptModels.EntityKey
                {
                    Id = entity.Id,
                    Type = entity.Type
                }
            };
            var result = await PlayFabCloudScriptAPI.ExecuteFunctionAsync(executeRequest);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(result.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ExecuteEventsForAllMembersOfClanAsync(string clanID, ProfileEventContainer eventContainer)
        {
            var clanMembersResult = await ClanModule.GetClanMembersAsync(clanID, new CBSProfileConstraints());
            if (clanMembersResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(clanMembersResult.Error);
            }

            var members = clanMembersResult.Result.Members ?? new List<ClanMember>();
            var ExecuteEventsTaskList = new List<Task<ExecuteResult<dynamic>>>();
            foreach (var member in members)
            {
                ExecuteEventsTaskList.Add(ExecuteProfileEventContainerAsync(member.ProfileID, eventContainer));
            }
            await Task.WhenAll(ExecuteEventsTaskList);

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ExecuteEventsForAllMembersOfTopClansAsync(string statisticName, int nTop, ProfileEventContainer eventContainer)
        {
            var learderboardRequest = new FunctionGetClanLeaderboardRequest
            {
                StatisticName = statisticName,
                Constraints = new CBSClanConstraints(),
                MaxCount = nTop
            };
            var clansLeaderboadResult = await ClanStatisticModule.GetClanLeaderboardAsync(learderboardRequest);
            if (clansLeaderboadResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(clansLeaderboadResult.Error);
            }
            var clanList = clansLeaderboadResult.Result.Leaderboard;
            var taskList = new List<Task<ExecuteResult<FunctionEmptyResult>>>();
            foreach (var clan in clanList)
            {
                taskList.Add(ExecuteEventsForAllMembersOfClanAsync(clan.ClanID, eventContainer));
            }
            await Task.WhenAll(taskList);

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }
    }
}