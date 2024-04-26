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
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System;
using Medallion.Threading.Azure;
using Medallion.Threading;
using PlayFab.ServerModels;

namespace CBS
{
    public class BattlePassModule : BaseAzureModule
    {
        private static readonly string LockPrefix = "battlepass"; 

        [FunctionName(AzureFunctions.StartBattlePassInstanceMethod)]
        public static async Task<dynamic> StartBattlePassInstanceTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var battlePassID = request.ID;

            var startResult = await StartBattlePassInstance(battlePassID, starter);
            if (startResult.Error != null)
            {
                return ErrorHandler.ThrowError(startResult.Error).AsFunctionResult();
            }

            return startResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.StopBattlePassInstanceMethod)]
        public static async Task<dynamic> StopBattlePassInstanceTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var itemID = request.ID;

            var stopResult = await StopBattlePassInstance(itemID, starter);
            if (stopResult.Error != null)
            {
                return ErrorHandler.ThrowError(stopResult.Error).AsFunctionResult();
            }

            return stopResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileBattlePassStatesMethod)]
        public static async Task<dynamic> GetProfileBattlePassStatesTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBattlePassRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var timeZone = request.TimeZone;

            var getResult = await GetProfileBattlePassStatesAsync(profileID, battlePassID, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetBattlePassFullInformationMethod)]
        public static async Task<dynamic> GetBattlePassFullInformationTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBattlePassRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var timeZone = request.TimeZone;

            var getResult = await GetBattlePassFullInformationAsync(profileID, battlePassID, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddBattlePassExpirienceMethod)]
        public static async Task<dynamic> AddBattlePassExpirienceTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionAddPassExpRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var expToAdd = request.ExpToAdd;
            var timeZone = request.TimeZone;

            var addResult = await AddBattlePassExpirienceAsync(profileID, battlePassID, expToAdd, timeZone);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError(addResult.Error).AsFunctionResult();
            }

            return addResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetRewardFromBattlePassInstanceMethod)]
        public static async Task<dynamic> GetRewardFromBattlePassInstanceTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetPassRewardRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var levelIndex = request.LevelIndex;
            var isPremium = request.IsPremium;
            var timeZone = request.TimeZone;

            var getResult = await GetRewardFromBattlePassInstanceAsync(profileID, battlePassID, levelIndex, isPremium, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ResetPlayerStateForBattlePassMethod)]
        public static async Task<dynamic> ResetPlayerStateForBattlePassTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBattlePassRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var timeZone = request.TimeZone;

            var resetResult = await ResetPlayerStateForBattlePassAsync(profileID, battlePassID, timeZone);
            if (resetResult.Error != null)
            {
                return ErrorHandler.ThrowError(resetResult.Error).AsFunctionResult();
            }

            return resetResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PreTicketPurchaseProccessMethod)]
        public static async Task<dynamic> PreTicketPurchaseProccessTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionTicketRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var ticketID = request.TicketID;
            var timeZone = request.TimeZone;

            var validateResult = await PrePurchaseTicketProccesAsync(profileID, battlePassID, ticketID, timeZone);
            if (validateResult.Error != null)
            {
                return ErrorHandler.ThrowError(validateResult.Error).AsFunctionResult();
            }

            return validateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PostTicketPurchaseProccessMethod)]
        public static async Task<dynamic> PostPurchaseProccessTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionTicketRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var ticketID = request.TicketID;

            var validateResult = await PostPurchaseTicketProccesAsync(profileID, battlePassID, ticketID);
            if (validateResult.Error != null)
            {
                return ErrorHandler.ThrowError(validateResult.Error).AsFunctionResult();
            }

            return validateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GrantTicketMethod)]
        public static async Task<dynamic> GrantTicketTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionTicketRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var ticketID = request.TicketID;
            var timeZone = request.TimeZone;

            var grantResult = await GrantTicketAsync(profileID, battlePassID, ticketID, timeZone);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError(grantResult.Error).AsFunctionResult();
            }

            return grantResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetBattlePassTasksMethod)]
        public static async Task<dynamic> GetBattlePassTasksTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBattlePassTasksRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var timeZone = request.TimeZone;

            var getResult = await GetBattlePassTasksForProfileAsync(profileID, battlePassID, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddBattlePassTaskPointsMethod)]
        public static async Task<dynamic> AddBattlePassTaskPointsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBattlePassAddTaskPointsRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var timeZone = request.TimeZone;
            var taskID = request.TaskID;
            var points = request.Points;

            var addResult = await AddBattlePassTaskPointsAsync(profileID, battlePassID, taskID, points, timeZone);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError(addResult.Error).AsFunctionResult();
            }

            return addResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PickupBattlePassTaskRewardMethod)]
        public static async Task<dynamic> PickupBattlePassTaskRewardTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBattlePassTasksRequest>();
            var profileID = request.ProfileID;
            var battlePassID = request.BattlePassID;
            var timeZone = request.TimeZone;
            var taskID = request.TaskID;

            var grantResult = await PickupBattlePassTaskRewardAsync(profileID, battlePassID, taskID, timeZone);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError(grantResult.Error).AsFunctionResult();
            }

            return grantResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureQueues.BattlePassPostProcessQueue)]
        public static async Task BattlePassPostProcessQueueTrigger([QueueTrigger(AzureQueues.BattlePassQueueName, Connection = StorageConnectionKey)] string argsString, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(argsString);
            var request = context.GetRequest<QueueGrantBankRewardRequest>();
            var battlePassID = request.BattlePassID;
            var instanceID = request.BattlePassInstanceID;

            await TableBattlePassAssistant.GrantBankRewardsAsync(battlePassID, instanceID);
            await TableBattlePassAssistant.GrantRewardOnEndAsync(battlePassID, instanceID);
        }

        public static async Task<ExecuteResult<FunctionStartBattlePassResult>> StartBattlePassInstance(string battlePassID, IDurableOrchestrationClient starter)
        {
            // get battle pass data
            var battlePassDataResult = await GetBattlePassDataAsync();
            if (battlePassDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionStartBattlePassResult>(battlePassDataResult.Error);
            }
            var battlePassData = battlePassDataResult.Result;
            var instances = battlePassData.Instances ?? new List<BattlePassInstance>();
            var instance = instances.FirstOrDefault(x=>x.ID == battlePassID);
            if (instance == null)
            {
                return ErrorHandler.BattlePassNotFound<FunctionStartBattlePassResult>();
            }
            // enable instance
            instance.IsActive = true;

            var eventID = instance.GetEventID();
            var eventSeconds = instance.DurationInHours * 60 * 60;

            var durableRequest = new FunctionDurableTaskRequest
            {
                EventID = eventID,
                Delay = eventSeconds,
                FunctionName = AzureFunctions.StopBattlePassInstanceMethod,
                FunctionRequest = new FunctionIDRequest
                {
                    ID = battlePassID
                }
            };
            var durableResult = await DurableTaskExecuter.StartDurableTaskAsync(durableRequest, starter);
            if (durableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionStartBattlePassResult>(durableResult.Error);
            }
            var passInstanceID = durableResult.Result.DurableTaskInstanceID;
            var endDate = durableResult.Result.ExecuteDate;

            instance.InstanceID = passInstanceID;
            instance.EndDate = endDate;
            instance.StartDate = ServerTimeUTC;

            // save changes          
            var rawData = JsonPlugin.ToJsonCompress(battlePassData);
            var updateRequest = new PlayFab.ServerModels.SetTitleDataRequest
            {
                Key = TitleKeys.BattlePassDataKey,
                Value = rawData
            };
            var updateResult = await FabServerAPI.SetTitleInternalDataAsync(updateRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionStartBattlePassResult>(updateResult.Error);
            }

            return new ExecuteResult<FunctionStartBattlePassResult>
            {
                Result = new FunctionStartBattlePassResult
                {
                    BattlePassID = battlePassID,
                    InstanceID = passInstanceID,
                    EndDate = endDate
                }
            };
        }

        public static async Task<ExecuteResult<FunctionStopBattlePassResult>> StopBattlePassInstance(string battlePassID, IDurableOrchestrationClient starter)
        {
            // get battle pass data
            var battlePassDataResult = await GetBattlePassDataAsync();
            if (battlePassDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionStopBattlePassResult>(battlePassDataResult.Error);
            }
            var battlePassData = battlePassDataResult.Result;
            var instances = battlePassData.Instances ?? new List<BattlePassInstance>();
            var instance = instances.FirstOrDefault(x=>x.ID == battlePassID);
            if (instance == null)
            {
                return ErrorHandler.BattlePassNotFound<FunctionStopBattlePassResult>();
            }
            if (!instance.IsActive)
            {
                return ErrorHandler.TaskAlreadyStopped<FunctionStopBattlePassResult>();
            }
            // disable item
            var instanceID = instance.InstanceID;
            instance.IsActive = false;
            instance.InstanceID = null;

            // stop task
            var eventID = instance.GetEventID();
            await DurableTaskExecuter.StopDurableTaskAsync(instanceID, eventID, starter);

            // save changes
            var rawData = JsonPlugin.ToJsonCompress(battlePassData);
            var updateRequest = new PlayFab.ServerModels.SetTitleDataRequest
            {
                Key = TitleKeys.BattlePassDataKey,
                Value = rawData
            };
            var updateResult = await FabServerAPI.SetTitleInternalDataAsync(updateRequest);

            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionStopBattlePassResult>(updateResult.Error);
            }

            await CreateQueueIfNotExistAsync(AzureQueues.BattlePassQueueName);
            var queueRequest = new QueueGrantBankRewardRequest
            {
                BattlePassID = battlePassID,
                BattlePassInstanceID = instanceID
            };
            await EventModule.ExecuteFunctionTriggerAsync(queueRequest, AzureQueues.BattlePassPostProcessQueue);

            return new ExecuteResult<FunctionStopBattlePassResult>
            {
                Result = new FunctionStopBattlePassResult
                {
                    BattlePassID = battlePassID,
                    InstanceID = instanceID,
                }
            };
        }

        public static async Task<ExecuteResult<FunctionBattlePassStatesResult>> GetProfileBattlePassStatesAsync(string profileID, string battlePassID, int timeZone)
        {
            var loadAll = string.IsNullOrEmpty(battlePassID);
            var instances = new BattlePassInstance [] {};
            if (loadAll)
            {
                var instancesResult = await LoadActiveBattlePassInstancesAsync();
                if (instancesResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionBattlePassStatesResult>(instancesResult.Error);
                }
                instances = instancesResult.Result;
            }
            else
            {
                var instanceResult = await LoadBattlePassInstnanceByIDAsync(battlePassID);
                if (instanceResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionBattlePassStatesResult>(instanceResult.Error);
                }
                var instance = instanceResult.Result;
                instances = new BattlePassInstance [] { instance };
            }
            var userStatesResult = await LoadBattlePassUserStates(profileID, instances, timeZone);
            if (userStatesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBattlePassStatesResult>(userStatesResult.Error);
            }
            var userStates = userStatesResult.Result;

            return new ExecuteResult<FunctionBattlePassStatesResult>
            {
                Result = new FunctionBattlePassStatesResult
                {
                    ProfileStates = userStates.Select(x=>x.Value).ToList()
                }
            };
        }

        public static async Task<ExecuteResult<FunctionBattlePassFullInfoResult>> GetBattlePassFullInformationAsync(string profileID, string battlePassID, int timeZone, string requestedInstanceID = null)
        {
            var instanceResult = await LoadBattlePassInstnanceByIDAsync(battlePassID);
            if (instanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBattlePassFullInfoResult>(instanceResult.Error);
            }
            var passInstance = instanceResult.Result;
            if (passInstance != null)
            {
                if (!string.IsNullOrEmpty(requestedInstanceID))
                {
                    passInstance.InstanceID = requestedInstanceID;
                }
                var userStatesResult = await LoadBattlePassUserStates(profileID, new BattlePassInstance [] {passInstance}, timeZone);
                if (userStatesResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionBattlePassFullInfoResult>(userStatesResult.Error);
                }
                var userStates = userStatesResult.Result;
                var state = userStates.FirstOrDefault().Value;
                return new ExecuteResult<FunctionBattlePassFullInfoResult>
                {
                    Result = new FunctionBattlePassFullInfoResult
                    {
                        Instance = passInstance,
                        ProfileState = state
                    }
                };
            }
            else
            {
                return new ExecuteResult<FunctionBattlePassFullInfoResult>
                {
                    Result = new FunctionBattlePassFullInfoResult()
                };
            }
        }

        public static async Task<ExecuteResult<FunctionAddPassAddExpResult>> AddBattlePassExpirienceAsync(string profileID, string battlePassID, int exp, int timeZone)
        {
            var addToAll = string.IsNullOrEmpty(battlePassID);
            var instancesToAdd = new BattlePassInstance [] {};
            if (addToAll)
            {
                var instancesResult = await LoadActiveBattlePassInstancesAsync();
                if (instancesResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionAddPassAddExpResult>(instancesResult.Error);
                }
                instancesToAdd = instancesResult.Result ?? new BattlePassInstance [] {};
            }
            else
            {
                var instanceResult = await LoadBattlePassInstnanceByIDAsync(battlePassID);
                if (instanceResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionAddPassAddExpResult>(instanceResult.Error);
                }
                var instance = instanceResult.Result;
                instancesToAdd = new BattlePassInstance [] {instance};
            }
            var addResult = await AddExpToBattlePassInstanceAsync(profileID, exp, instancesToAdd, timeZone);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAddPassAddExpResult>(addResult.Error);
            }
            var expTable = addResult.Result;

            return new ExecuteResult<FunctionAddPassAddExpResult>
            {
                Result = new FunctionAddPassAddExpResult
                {
                    ExpTable = expTable
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ResetPlayerStateForBattlePassAsync(string profileID, string battlePassID, int timeZone)
        {
            var instanceResult = await LoadBattlePassInstnanceByIDAsync(battlePassID);
            if (instanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(instanceResult.Error);
            }
            var instance = instanceResult.Result;
            var userStatesResult = await LoadBattlePassUserStates(profileID, new BattlePassInstance [] {instance}, timeZone);
            if (userStatesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(userStatesResult.Error);
            }
            var userStates = userStatesResult.Result ?? new Dictionary<string, BattlePassUserInfo>();
            var state = userStates.ContainsKey(battlePassID) ? userStates[battlePassID] : new BattlePassUserInfo();

            var userStatesDataResult = await GetProfileBattlePassDataAsync(profileID);
            if (userStatesDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(userStatesDataResult.Error);
            }
            var userStatesData = userStatesDataResult.Result;
            var userDataTable = userStatesData.States ?? new Dictionary<string, BattlePassUserData>();
            var stateData = new BattlePassUserData();
            userDataTable[battlePassID] = stateData;
            userStatesData.States = userDataTable;

            userStates[battlePassID] = state;
            var saveResult = await SaveProfileBattlePassDataAsync(profileID, userStatesData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionBattlePassGrantRewardResult>> GetRewardFromBattlePassInstanceAsync(string profileID, string battlePassID, int level, bool isPremium, int timeZone)
        {
            var instancesResult = await LoadBattlePassInstnanceByIDAsync(battlePassID);
            if (instancesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBattlePassGrantRewardResult>(instancesResult.Error);
            }
            var instance = instancesResult.Result;
            var userStatesResult = await LoadBattlePassUserStates(profileID, new BattlePassInstance [] {instance}, timeZone);
            if (userStatesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBattlePassGrantRewardResult>(userStatesResult.Error);
            }
            var userStates = userStatesResult.Result;
            var state = userStates.ContainsKey(battlePassID) ? userStates[battlePassID] : new BattlePassUserInfo();
            var levels = instance.GetLevelTree();
            if (levels == null || levels.Count == 0 || level > levels.Count)
            {
                return ErrorHandler.RewardNotFound<FunctionBattlePassGrantRewardResult>();
            }

            var levelObject = levels[level];
            var rewardType = isPremium ? BattlePassRewardType.PREMIUM : BattlePassRewardType.DEFAULT;
            var levelParser = new  BattlePassLevelInfo(instance, state, levelObject, level, instance.GetMaxLevelCount());
            var awardToGrant = levelParser.GetReward(rewardType);
            var rewardAvailable = levelParser.IsRewardAvailableToCollect(rewardType);
            if (awardToGrant == null || !rewardAvailable || !instance.IsActive)
            {
                return ErrorHandler.RewardNotAvailable<FunctionBattlePassGrantRewardResult>();
            }

            var playerRecivedReward = isPremium ? state.CollectedPremiumReward : state.CollectedSimpleReward;
            playerRecivedReward = playerRecivedReward ?? new int[]{};
            var playerRecivedRewardList = playerRecivedReward.ToList();
            if (playerRecivedReward.Contains(level))
            {
                return ErrorHandler.AlreadyRewarded<FunctionBattlePassGrantRewardResult>();
            }

            var rewardResult = await RewardModule.GrantRewardToProfileAsync(awardToGrant, profileID);
            if (rewardResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBattlePassGrantRewardResult>(rewardResult.Error);
            }
            var grantResult = rewardResult.Result;

            playerRecivedRewardList.Add(level);

            var userStatesDataResult = await GetProfileBattlePassDataAsync(profileID);
            if (userStatesDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBattlePassGrantRewardResult>(userStatesDataResult.Error);
            }
            var userStatesData = userStatesDataResult.Result;
            var userDataTable = userStatesData.States ?? new Dictionary<string, BattlePassUserData>();
            var stateData = userDataTable.ContainsKey(battlePassID) ? userDataTable[battlePassID] : new BattlePassUserData();

            if (isPremium)
            {
                stateData.CollectedPremiumReward = playerRecivedRewardList;
            }
            else
            {
                stateData.CollectedSimpleReward = playerRecivedRewardList;
            }

            userDataTable[battlePassID] = stateData;
            userStatesData.States = userDataTable;
            var saveResult = await SaveProfileBattlePassDataAsync(profileID, userStatesData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBattlePassGrantRewardResult>(saveResult.Error);
            }

            return new ExecuteResult<FunctionBattlePassGrantRewardResult>
            {
                Result = new FunctionBattlePassGrantRewardResult{
                    InstanceID = battlePassID,
                    IsPremium = isPremium,
                    RewardResult = grantResult
                }
            };
        }

        public static async Task<ExecuteResult<Dictionary<string, int>>> AddExpToBattlePassInstanceAsync(string profileID, int exp, BattlePassInstance [] instances, int timeZone)
        {
            var userStatesDataResult = await GetProfileBattlePassDataAsync(profileID);
            if (userStatesDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<Dictionary<string, int>>(userStatesDataResult.Error);
            }
            var userStatesData = userStatesDataResult.Result;
            var userStates = userStatesData.States ?? new Dictionary<string, BattlePassUserData>();

            // check is instance is valid
            var isValid = true;
            for (int i=0;i<instances.Length;i++)
            {
                var instance = instances[i];
                var battlePassID = instance.ID;
                if (!userStates.ContainsKey(battlePassID) || userStates[battlePassID].InstanceID != instance.InstanceID)
                {
                    isValid = false;
                }
            }
            if (!isValid)
            {
                var checkResult = await GetProfileBattlePassStatesAsync(profileID, null, timeZone);
                if (checkResult.Error != null)
                {
                    return ErrorHandler.ThrowError<Dictionary<string, int>>(checkResult.Error);
                }
                var doubleCheckResult = await GetProfileBattlePassDataAsync(profileID);
                if (doubleCheckResult.Error != null)
                {
                    return ErrorHandler.ThrowError<Dictionary<string, int>>(doubleCheckResult.Error);
                }
                var doubleCheckStatesData = doubleCheckResult.Result;
                userStates = doubleCheckStatesData.States ?? new Dictionary<string, BattlePassUserData>();
            }

            // check if instance is active
            var activeInstances = instances.Where(x=>x.IsActive).ToArray();
            var resultTable = new Dictionary<string, int>();
            for (int i = 0; i < activeInstances.Length; i++)
            {
                var instance = activeInstances[i];
                var instanceID = instance.InstanceID;
                var maxExp = instance.GetMaxExpValue();
                var battlePassID = instance.ID;
                if (userStates.ContainsKey(battlePassID))
                {
                    var expMultiply = userStates[battlePassID].ExpMultiply;
                    exp = (int)((float)exp * expMultiply);
                    var currentExp = userStates[battlePassID].CurrentExp;
                    currentExp+=exp;
                    if (currentExp > maxExp)
                        currentExp = maxExp;
                    userStates[battlePassID].CurrentExp=currentExp;
                }
                else
                {
                    userStates[battlePassID] = new BattlePassUserData();
                    var currentExp = userStates[battlePassID].CurrentExp;
                    currentExp+=exp;
                    if (currentExp > maxExp)
                        currentExp = maxExp;
                    userStates[battlePassID].CurrentExp=currentExp;
                }
                resultTable[battlePassID] = exp;

                var hasBankAccess = userStates[battlePassID].BankAccess;
                if (hasBankAccess)
                {
                    var currentExp = userStates[battlePassID].CurrentExp;
                    var addBankValueResult = await TableBattlePassAssistant.UpdateBankExpAsync(profileID, instanceID, currentExp);
                    if (addBankValueResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<Dictionary<string, int>>(addBankValueResult.Error);
                    }
                }

                var automaticReward = instance.AutomaticRewardOnEnd;
                if (automaticReward)
                {
                    var checkInResult = await TableBattlePassAssistant.BattlePassCheckInAsync(profileID, instanceID);
                    if (checkInResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<Dictionary<string, int>>(checkInResult.Error);
                    }
                }
            }
            userStatesData.States = userStates;

            var saveResult = await SaveProfileBattlePassDataAsync(profileID, userStatesData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<Dictionary<string, int>>(saveResult.Error);
            }

            return new ExecuteResult<Dictionary<string, int>>
            {
                Result = resultTable
            };
        }

        public static async Task<ExecuteResult<Dictionary<string, BattlePassUserInfo>>> LoadBattlePassUserStates(string profileID, BattlePassInstance[] instances, int timeZone)
        {
            var userStatesResult = new Dictionary<string, BattlePassUserInfo>();
            var instanceCount = instances.Length;
            var userStatesDataResult = await GetProfileBattlePassDataAsync(profileID);
            if (userStatesDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<Dictionary<string, BattlePassUserInfo>>(userStatesDataResult.Error);
            }
            var userStatesData = userStatesDataResult.Result;
            var userStates = userStatesData.States ?? new Dictionary<string, BattlePassUserData>();

            var resetRequest = false;
            var ticketdIDsToRemove = new List<string>();
            
            for (int i=0;i<instanceCount;i++)
            {
                var passInstance = instances[i];
                var passID = passInstance.ID;
                var passInstanceID = passInstance.InstanceID;
                var userState = userStates.ContainsKey(passID) ? userStates[passID] : new BattlePassUserData();
                var userResult = ParseUserBattlePassResult(profileID, passInstance, userState);
                var userInfo = userResult.UserInfo;
                if (userResult.ResetRequest)
                {
                    userState = new BattlePassUserData();
                    userState.InstanceID = userInfo.InstanceID;
                    userState.BattlePassID = userInfo.BattlePassID;
                    userResult = ParseUserBattlePassResult(profileID, passInstance, userState);           
                    var freeTicket = passInstance.GetFreeTicket();
                    // apply ticket properties
                    var applyResult = await ApplyTicketPropertiesAsync(profileID, freeTicket, passInstance, userState);
                    if (applyResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<Dictionary<string, BattlePassUserInfo>>(applyResult.Error);
                    }
                    if (!freeTicket.OverrideExpMultiply)
                    {
                        freeTicket.ExpMultiply = 1f;
                        userState.ExpMultiply = 1f;
                    }
                    var paidTickets = passInstance.GetPaidTickets();
                    foreach (var ticket in paidTickets)
                    {
                        ticketdIDsToRemove.Add(ticket.GetCatalogID());
                    }
                    // generate limit start date
                    var instanceStartDate = passInstance.StartDate.GetValueOrDefault();
                    var requestDate = instanceStartDate.AddMilliseconds(timeZone);
                    var nextDayDate = TimePeriodAssistant.DateOfNextCheckIn(requestDate, DatePeriod.Day);
                    var limitDate = nextDayDate.GetValueOrDefault().AddDays(-1);
                    userState.LimitStartDate = limitDate;
                    userStates[passID] = userState;

                    resetRequest = true;
                }
                userStatesResult[passID] = userInfo;
            }

            if (resetRequest)
            {
                var lockID = LockPrefix + profileID;
                var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
                await CreateLockContainerIfNotExistAsync();
                await using (await locker.AcquireLockAsync(lockID))
                {
                    await using var handle = await locker.TryAcquireLockAsync(lockID);  
                    // remove tickets
                    var inventoryRequest = new GetUserInventoryRequest
                    {
                        PlayFabId = profileID
                    };
                    var getInventoryResult = await FabServerAPI.GetUserInventoryAsync(inventoryRequest);
                    if (getInventoryResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionEmptyResult>(getInventoryResult.Error);
                    }
                    var inventory = getInventoryResult.Result.Inventory ?? new List<ItemInstance>();
                    var battlePassInventory = inventory.Where(x=>x.CatalogVersion == CatalogKeys.BattlePassCatalogID);
                    var inventoryTicketsIDs = battlePassInventory.Where(x=> ticketdIDsToRemove.Contains(x.ItemId)).Select(x=>x.ItemInstanceId).ToArray();

                    var revokeList = inventoryTicketsIDs.Select(x=> new RevokeInventoryItem
                    {
                        PlayFabId = profileID,
                        ItemInstanceId = x
                    }).ToList();
                    var revokeRequest = new RevokeInventoryItemsRequest
                    {
                        Items = revokeList
                    };
                    var revokeResult = await FabServerAPI.RevokeInventoryItemsAsync(revokeRequest);

                    userStatesData.States = userStates;
                    var saveResult = await SaveProfileBattlePassDataAsync(profileID, userStatesData);
                    if (saveResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<Dictionary<string, BattlePassUserInfo>>(saveResult.Error);
                    }
                }
            }

            return new ExecuteResult<Dictionary<string, BattlePassUserInfo>>
            {
                Result = userStatesResult
            };
        }

        private static async Task<ExecuteResult<BattlePassUserStatesData>> GetProfileBattlePassDataAsync(string profileID)
        {
            var dataResult = await GetProfileInternalDataAsObject<BattlePassUserStatesData>(profileID, ProfileDataKeys.ProfileBattleDataKey);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<BattlePassUserStatesData>(dataResult.Error);
            }
            return new ExecuteResult<BattlePassUserStatesData>
            {
                Result = dataResult.Result ?? new BattlePassUserStatesData()
            };
        }

        public static async Task<ExecuteResult<BattlePassInstance>> PrePurchaseTicketProccesAsync(string profileID, string battlePassID, string ticketID, int timeZone)
        {
            var instanceResult = await LoadBattlePassInstnanceByIDAsync(battlePassID);
            if (instanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<BattlePassInstance>(instanceResult.Error);
            }
            var instance = instanceResult.Result;
            var userStatesResult = await LoadBattlePassUserStates(profileID, new BattlePassInstance[] {instance}, timeZone);
            if (userStatesResult.Error != null)
            {
                return ErrorHandler.ThrowError<BattlePassInstance>(userStatesResult.Error);
            }
            var userStates = userStatesResult.Result;
            var state = userStates.ContainsKey(battlePassID) ? userStates[battlePassID] : new BattlePassUserInfo();

            // check if instance is active
            if (!instance.IsActive)
            {
                return ErrorHandler.BattlePassNotActive<BattlePassInstance>();
            }

            // check is ticket exist
            var paidTickets = instance.GetPaidTickets();
            var ticket = paidTickets.FirstOrDefault(x=>x.ID == ticketID);
            if (ticket == null)
            {
                return ErrorHandler.TicketNotFound<BattlePassInstance>();
            }

            // check is ticket already purchased
            var inventoryRequest = new GetUserInventoryRequest
            {
                PlayFabId = profileID
            };
            var getInventoryResult = await FabServerAPI.GetUserInventoryAsync(inventoryRequest);
            if (getInventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<BattlePassInstance>(getInventoryResult.Error);
            }
            var inventory = getInventoryResult.Result.Inventory ?? new List<ItemInstance>();
            var battlePassInventory = inventory.Where(x=>x.CatalogVersion == CatalogKeys.BattlePassCatalogID);

            var purchaseType = ticket.PurchaseType;
            if (purchaseType == CBSPurchaseType.NOT_CONSUMABLE)
            {
                var purchased = battlePassInventory.Any(x=>x.ItemId == ticket.GetCatalogID());
                if (purchased)
                {
                    return ErrorHandler.AlreadyPurchasedError<BattlePassInstance>();
                }
            }

            return new ExecuteResult<BattlePassInstance>
            {
                Result = instance
            };
        }

        private static async Task<ExecuteResult<FunctionPostPurchaseTicketResult>> PostPurchaseTicketProccesAsync(string profileID, string battlePassID, string ticketID)
        {
            // get instance
            var instanceResult = await LoadBattlePassInstnanceByIDAsync(battlePassID);
            if (instanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPostPurchaseTicketResult>(instanceResult.Error);
            }
            var instance = instanceResult.Result;
            var instanceID = instance.InstanceID;
            var tickets = instance.GetPaidTickets();
            var ticket = tickets.FirstOrDefault(x=>x.ID == ticketID);
            if (ticket == null)
            {
                return ErrorHandler.TicketNotFound<FunctionPostPurchaseTicketResult>();
            }
            var ticketCatalogID = ticket.GetCatalogID();

            // get state
            var userStatesDataResult = await GetProfileBattlePassDataAsync(profileID);
            if (userStatesDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPostPurchaseTicketResult>(userStatesDataResult.Error);
            }
            var userStatesData = userStatesDataResult.Result;
            var userStates = userStatesData.States ?? new Dictionary<string, BattlePassUserData>();
            var userState = userStates.ContainsKey(battlePassID) ? userStates[battlePassID] : new BattlePassUserData();

            // apply ticket properties
            var applyResult = await ApplyTicketPropertiesAsync(profileID, ticket, instance, userState);
            if (applyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPostPurchaseTicketResult>(applyResult.Error);
            }
            userState = applyResult.Result;
            userStates[battlePassID] = userState;
            userStatesData.States = userStates;

            // save result
            var saveResult = await SaveProfileBattlePassDataAsync(profileID, userStatesData);
            if (saveResult.Error != null)
            {
                ErrorHandler.ThrowError<FunctionPostPurchaseTicketResult>(saveResult.Error);
            }

            return new ExecuteResult<FunctionPostPurchaseTicketResult>
            {
                Result = new FunctionPostPurchaseTicketResult
                {
                    BattlePassID = battlePassID,
                    BattlePassInstanceID = instanceID,
                    TicketID = ticketID,
                    TicketCatalogID = ticketCatalogID,
                    Ticket = ticket
                }
            };
        }

        private static async Task<ExecuteResult<FunctionEmptyResult>> SaveProfileBattlePassDataAsync(string profileID, BattlePassUserStatesData newData)
        {
            var rawData = JsonPlugin.ToJsonCompress(newData);
            var saveResult = await SaveProfileInternalDataAsync(profileID, ProfileDataKeys.ProfileBattleDataKey, rawData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        private static async Task<ExecuteResult<BattlePassInstance[]>> LoadAllBattlePassInstancesAsync()
        {
            var battlePassDataResult = await GetBattlePassDataAsync();
            if (battlePassDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<BattlePassInstance[]>(battlePassDataResult.Error);
            }
            var battlePassData = battlePassDataResult.Result;
            var instances =  battlePassData.Instances ?? new List<BattlePassInstance>();
            return new ExecuteResult<BattlePassInstance[]>
            {
                Result = instances.ToArray()
            };
        }

        private static async Task<ExecuteResult<BattlePassInstance[]>> LoadActiveBattlePassInstancesAsync()
        {
            var battlePassDataResult = await GetBattlePassDataAsync();
            if (battlePassDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<BattlePassInstance[]>(battlePassDataResult.Error);
            }
            var battlePassData = battlePassDataResult.Result;
            var dataArray = battlePassData.Instances.Where(x=>x.IsActive).ToList();
            return new ExecuteResult<BattlePassInstance[]>
            {
                Result = dataArray.ToArray()
            };
        }

        public static async Task<ExecuteResult<BattlePassInstance>> LoadBattlePassInstnanceByIDAsync(string battlePassID)
        {
            var battlePassDataResult = await GetBattlePassDataAsync();
            if (battlePassDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<BattlePassInstance>(battlePassDataResult.Error);
            }
            var battlePassData = battlePassDataResult.Result;
            var instance = battlePassData.Instances.FirstOrDefault(x=>x.ID == battlePassID);
            if (instance == null)
            {
                ErrorHandler.BattlePassNotFound<BattlePassInstance>();
            }
            return new ExecuteResult<BattlePassInstance>
            {
                Result = instance
            };
        }

        private static async Task<ExecuteResult<BattlePassData>> GetBattlePassDataAsync()
        {
            var dataResult = await GetInternalTitleDataAsObjectAsync<BattlePassData>(TitleKeys.BattlePassDataKey);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<BattlePassData>(dataResult.Error);
            }
            return new ExecuteResult<BattlePassData>
            {
                Result = dataResult.Result ?? new BattlePassData()
            };
        }

        private static async Task<ExecuteResult<FunctionGrantTicketResult>> GrantTicketAsync(string profileID, string battlePassID, string ticketID, int timeZone)
        {
            var prePurchaseResult = await PrePurchaseTicketProccesAsync(profileID, battlePassID, ticketID, timeZone);
            if (prePurchaseResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantTicketResult>(prePurchaseResult.Error);
            }
            var instance = prePurchaseResult.Result;
            var instanceID = instance.InstanceID;
            var tickets = instance.GetPaidTickets();
            var ticket = tickets.FirstOrDefault(x=>x.ID == ticketID);
            var ticketCatalogID = ticket.GetCatalogID();

            // get state
            var userStatesDataResult = await GetProfileBattlePassDataAsync(profileID);
            if (userStatesDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantTicketResult>(userStatesDataResult.Error);
            }
            var userStatesData = userStatesDataResult.Result;
            var userStates = userStatesData.States ?? new Dictionary<string, BattlePassUserData>();
            var userState = userStates.ContainsKey(battlePassID) ? userStates[battlePassID] : new BattlePassUserData();

            // grant item
            var grantResult = await InternalGrantItemsToPlayerAsync(CatalogKeys.BattlePassCatalogID, new List<string>{ticketCatalogID}, profileID);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantTicketResult>(grantResult.Error);
            }
            var grantList = grantResult.Result;
            var itemResult = grantList.ItemGrantResults.FirstOrDefault();

            var applyResult = await ApplyTicketPropertiesAsync(profileID, ticket, instance, userState);
            if (applyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantTicketResult>(applyResult.Error);
            }
            userState = applyResult.Result;
            userStates[battlePassID] = userState;
            userStatesData.States = userStates;

            var saveResult = await SaveProfileBattlePassDataAsync(profileID, userStatesData);
            if (saveResult.Error != null)
            {
                ErrorHandler.ThrowError<FunctionGrantTicketResult>(saveResult.Error);
            }

            return new ExecuteResult<FunctionGrantTicketResult>
            {
                Result = new FunctionGrantTicketResult
                {
                    BattlePassID = battlePassID,
                    BattlePassInstanceID = instanceID,
                    TicketID = ticketID,
                    TicketCatalogID = ticketCatalogID,
                    Ticket = ticket
                }
            };
        }

        public static async Task<ExecuteResult<FunctionProfileTasksResult>> GetBattlePassTasksForProfileAsync(string profileID, string battlePassID, int timeZone)
        {
            var instanceResult = await LoadBattlePassInstnanceByIDAsync(battlePassID);
            if (instanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionProfileTasksResult>(instanceResult.Error);
            }
            var instance = instanceResult.Result;
            var userStatesResult = await LoadBattlePassUserStates(profileID, new BattlePassInstance[] {instance}, timeZone);
            if (userStatesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionProfileTasksResult>(userStatesResult.Error);
            }
            var userStates = userStatesResult.Result;
            var state = userStates.ContainsKey(battlePassID) ? userStates[battlePassID] : new BattlePassUserInfo();

            // check if instance is active
            if (!instance.IsActive)
            {
                return ErrorHandler.BattlePassNotActive<FunctionProfileTasksResult>();
            }

            var isTasksEnabled = instance.TasksEnabled;
            var tasksPoolID = instance.TasksPoolID;
            if (!isTasksEnabled || string.IsNullOrEmpty(tasksPoolID))
            {
                return ErrorHandler.TasksPoolNotFound<FunctionProfileTasksResult>();
            }

            var tasksAvailable = state.TasksAccess;
            if (!tasksAvailable)
            {
                return ErrorHandler.TasksNotAvalilable<FunctionProfileTasksResult>();
            }

            var tasksResult = await ProfileTaskModule.GetTasksForProfileAsync(profileID, tasksPoolID, timeZone);
            if (tasksResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionProfileTasksResult>(tasksResult.Error);
            }

            return new ExecuteResult<FunctionProfileTasksResult>
            {
                Result = tasksResult.Result
            };
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<CBSTask>>> AddBattlePassTaskPointsAsync(string profileID, string battlePassID, string taskID, int points, int timeZone)
        {
            var instanceResult = await LoadBattlePassInstnanceByIDAsync(battlePassID);
            if (instanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(instanceResult.Error);
            }
            var instance = instanceResult.Result;
            var userStatesResult = await LoadBattlePassUserStates(profileID, new BattlePassInstance[] {instance}, timeZone);
            if (userStatesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(userStatesResult.Error);
            }
            var userStates = userStatesResult.Result;
            var state = userStates.ContainsKey(battlePassID) ? userStates[battlePassID] : new BattlePassUserInfo();

            // check if instance is active
            if (!instance.IsActive)
            {
                return ErrorHandler.BattlePassNotActive<FunctionModifyTaskResult<CBSTask>>();
            }

            var isTasksEnabled = instance.TasksEnabled;
            var tasksPoolID = instance.TasksPoolID;
            if (!isTasksEnabled || string.IsNullOrEmpty(tasksPoolID))
            {
                return ErrorHandler.TasksPoolNotFound<FunctionModifyTaskResult<CBSTask>>();
            }

            var tasksAvailable = state.TasksAccess;
            if (!tasksAvailable)
            {
                return ErrorHandler.TasksNotAvalilable<FunctionModifyTaskResult<CBSTask>>();
            }

            var addPointsResult = await ProfileTaskModule.ModifyTaskPointsAsync(profileID, taskID, tasksPoolID, ModifyMethod.ADD, points, timeZone);
            if (addPointsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(addPointsResult.Error);
            }

            return new ExecuteResult<FunctionModifyTaskResult<CBSTask>>
            {
                Result = addPointsResult.Result
            };
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<CBSTask>>> PickupBattlePassTaskRewardAsync(string profileID, string battlePassID, string taskID, int timeZone)
        {
            var instanceResult = await LoadBattlePassInstnanceByIDAsync(battlePassID);
            if (instanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(instanceResult.Error);
            }
            var instance = instanceResult.Result;
            var userStatesResult = await LoadBattlePassUserStates(profileID, new BattlePassInstance[] {instance}, timeZone);
            if (userStatesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(userStatesResult.Error);
            }
            var userStates = userStatesResult.Result;
            var state = userStates.ContainsKey(battlePassID) ? userStates[battlePassID] : new BattlePassUserInfo();

            // check if instance is active
            if (!instance.IsActive)
            {
                return ErrorHandler.BattlePassNotActive<FunctionModifyTaskResult<CBSTask>>();
            }

            var isTasksEnabled = instance.TasksEnabled;
            var tasksPoolID = instance.TasksPoolID;
            if (!isTasksEnabled || string.IsNullOrEmpty(tasksPoolID))
            {
                return ErrorHandler.TasksPoolNotFound<FunctionModifyTaskResult<CBSTask>>();
            }

            var tasksAvailable = state.TasksAccess;
            if (!tasksAvailable)
            {
                return ErrorHandler.TasksNotAvalilable<FunctionModifyTaskResult<CBSTask>>();
            }

            var grantRewardResult = await ProfileTaskModule.PickupTaskRewardAsync(profileID, tasksPoolID, taskID);
            if (grantRewardResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(grantRewardResult.Error);
            }

            return new ExecuteResult<FunctionModifyTaskResult<CBSTask>>
            {
                Result = grantRewardResult.Result
            };
        }


        public static async Task<ExecuteResult<FunctionCatalogItemsResult>> GetTicketsCatalogItemsAsync()
        {
            var request = new GetCatalogItemsRequest
            {
                CatalogVersion = CatalogKeys.BattlePassCatalogID
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

        public static async Task<ExecuteResult<BattlePassUserData>> ApplyTicketPropertiesAsync(string profileID, BattlePassTicket ticket, BattlePassInstance instance, BattlePassUserData userData)
        {
            // apply exp multiply
            var overrideExpMultiply = ticket.OverrideExpMultiply;
            if (overrideExpMultiply)
            {
                var ticketMultiply = ticket.ExpMultiply;
                var dataMultiply = userData.ExpMultiply;
                if (ticketMultiply > dataMultiply)
                {
                    userData.ExpMultiply = ticketMultiply;
                }
            }
            // apply premium access
            var ticketPremiumAccess = ticket.PremiumAccess;
            if (ticketPremiumAccess)
            {
                userData.PremiumAccess = ticketPremiumAccess;
            }
            // apply tasks access
            var ticketTasksAccess = ticket.TasksAccess;
            if (ticketTasksAccess)
            {
                userData.TasksAccess = ticketTasksAccess;
            }
            // apply extra level access
            var ticketExtraLevelAccess = ticket.ExtraLevelAccess;
            if (ticketExtraLevelAccess)
            {
                userData.ExtraLevelAccess = ticketExtraLevelAccess;
            }
            // apply extra level access
            var ticketTimerLimitAccess = ticket.DisableTimeLimit;
            if (ticketTimerLimitAccess)
            {
                userData.DisableTimeLimit = ticketTimerLimitAccess;
            }
            // apply skip level
            var ticketSkipLevel = ticket.SkipLevel;
            if (ticketSkipLevel)
            {
                var levelsToAdd = ticket.SkipLevelCount;
                var expStep = instance.ExpStep;
                var expToAdd = expStep * levelsToAdd;
                var maxExp = instance.GetMaxExpValue();
                if ((userData.CurrentExp + expToAdd) > maxExp)
                {
                    userData.CurrentExp = maxExp;
                }
                else
                {
                    userData.AddExp(expToAdd);
                }
            }
            // apply bank access
            var ticketBankAccess = ticket.BankAccess;
            if (ticketBankAccess)
            {
                if (!userData.BankAccess)
                {
                    var passInstanceID = instance.InstanceID;
                    var addBankValueResult = await TableBattlePassAssistant.AddBankExpAsync(profileID, passInstanceID, userData.CurrentExp);
                    if (addBankValueResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<BattlePassUserData>(addBankValueResult.Error);
                    }
                }
                userData.BankAccess = ticketBankAccess;
            }
            // mark as purchased
            var purchaseType = ticket.PurchaseType;
            if (purchaseType == CBSPurchaseType.NOT_CONSUMABLE)
            {
                userData.AddPurchasedTicket(ticket.GetCatalogID());
            }

            return new ExecuteResult<BattlePassUserData>
            {
                Result = userData
            };
        }

        private static ParseUserInfoResult ParseUserBattlePassResult(string profileID, BattlePassInstance instance, BattlePassUserData userData)
        {
            // check new instance
            var instanceID = instance.InstanceID;
            var lastSavedID = userData.InstanceID;
            var saveRequest = lastSavedID != instanceID;
            if (saveRequest)
            {
                userData = new BattlePassUserData();
                userData.InstanceID = instanceID;
                userData.BattlePassID = instance.ID;
            }

            var collectedSimpleRewards = userData.CollectedSimpleReward;
            var collectedPremiumRewards = userData.CollectedPremiumReward;
            var passID = instance.ID;
            var passName = instance.DisplayName;
            var userExp = userData.CurrentExp;
            var passExpStep = instance.ExpStep;
            var expOfCurrentLevel = userExp % passExpStep;
            var customRawData = instance.CustomRawData;
            var customDataClass = instance.CustomDataClassName;
            var badgeCount = 0;
            var isActive = instance.IsActive;
            var endDate = instance.EndDate;
            var expMultiply = userData.ExpMultiply;
            var isPremium = userData.PremiumAccess;
            var isExtraLevel = userData.ExtraLevelAccess;
            var isBank = userData.BankAccess;
            var enableTask = userData.TasksAccess;
            var disableTimeLimit = userData.DisableTimeLimit;
            var purchasedTickets = userData.PurchasedTickets;
            var limitDate = userData.LimitStartDate;

            var levelTree = instance.GetLevelTree();
            var levelCount = instance.GetMaxLevelCount();
            var maxLevel = levelCount;
            var currentLevel = userExp/passExpStep;
            if (currentLevel > maxLevel)
                currentLevel = maxLevel;
            var reachMaxLevel = currentLevel == maxLevel;

            var userInfo = new BattlePassUserInfo{
                ProfileID = profileID,
                BattlePassID = passID,
                InstanceID = instanceID,
                BattlePassName = passName,
                PlayerLevel = currentLevel,
                PlayerExp = userExp,
                ExpOfCurrentLevel = expOfCurrentLevel,
                ExpStep = passExpStep,
                EndDate = endDate,
                LimitStartDate = limitDate,
                PremiumAccess = isPremium,
                ExpMultiply = expMultiply,
                ExtraLevelAccess = isExtraLevel,
                BankAccess = isBank,
                TasksAccess = enableTask,
                DisableTimeLimit = disableTimeLimit,
                CollectedSimpleReward = collectedSimpleRewards.ToArray(),
                CollectedPremiumReward = collectedPremiumRewards.ToArray(),
                PurchasedTickets = purchasedTickets.ToArray(),
                RewardBadgeCount = badgeCount,
                IsActive = isActive,
                CustomRawData = customRawData,
                CustomDataClassName = customDataClass
            };

            if (levelTree != null && levelTree.Count != 0 && isActive)
            {
                var levelParser = new  BattlePassLevelInfo(instance, userInfo, new BattlePassLevel(), 0, maxLevel);
                var levelsToCheck = reachMaxLevel ? levelCount -1 : currentLevel;
                for (int i=0;i<=levelsToCheck;i++)
                {
                    var level = levelTree[i];
                    levelParser.RebuildLevel(level, i);
                    var simpleReward = level.DefaultReward;
                    var premiumReward = level.PremiumReward;
                    if (simpleReward != null)
                    {
                        var isRewardAvailable = levelParser.IsRewardAvailableToCollect(BattlePassRewardType.DEFAULT);
                        if (isRewardAvailable)
                            badgeCount++;
                    }
                    if (premiumReward != null)
                    {
                        var isRewardAvailable = levelParser.IsRewardAvailableToCollect(BattlePassRewardType.PREMIUM);
                        if (isRewardAvailable)
                            badgeCount++;
                    }
                }
            }
            userInfo.RewardBadgeCount = badgeCount;

            return new ParseUserInfoResult
            {
                UserInfo = userInfo,
                ResetRequest = saveRequest
            };
        }
    }
}