using PlayFab.MultiplayerModels;
using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;

namespace CBS
{
    public class MatchmakingModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.GetMatchmakingQueueMethod)]
        public static async Task<dynamic> GetMatchmakingQueueTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionMatchmakingQueueRequest>();
            var queue = request.Queue;

            var getResult = await GetMatchmakingQueueAsync(queue);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetMatchmakingListMethod)]
        public static async Task<dynamic> GetMatchmakingListTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionMatchmakingQueueRequest>();

            var getResult = await GetMatchmakingListAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateMatchmakingQueueMethod)]
        public static async Task<dynamic> UpdateMatchmakingQueueTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionMatchmakingQueueRequest>();
            var queue = request.Queue;

            var updateResult = await UpdateMatchmakingQueueAsync(queue);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.RemoveMatchmakingQueueMethod)]
        public static async Task<dynamic> RemoveMatchmakingQueueTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionMatchmakingQueueRequest>();
            var queue = request.Queue;

            var removeResult = await RemoveMatchmakingQueueAsync(queue);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError(removeResult.Error).AsFunctionResult();
            }

            return removeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetMatchMethod)]
        public static async Task<dynamic> GetMatchTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionMatchRequest>();
            var queue = request.Queue;
            var matchID = request.MatchID;

            var getResult = await GetMatchAsync(queue, matchID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<GetMatchmakingQueueResult>> GetMatchmakingQueueAsync(string queueName)
        {
            var multiplayerApi = await GetFabMultiplayerAPIAsync();

            var request = new GetMatchmakingQueueRequest {
                QueueName = queueName
            };
            
            var getQueueResult = await multiplayerApi.GetMatchmakingQueueAsync(request);
            if (getQueueResult.Error != null)
            {
                return ErrorHandler.ThrowError<GetMatchmakingQueueResult>(getQueueResult.Error);
            }

            return new ExecuteResult<GetMatchmakingQueueResult>
            {
                Result = getQueueResult.Result
            };
        }

        public static async Task<ExecuteResult<ListMatchmakingQueuesResult>> GetMatchmakingListAsync()
        {
            var multiplayerApi = await GetFabMultiplayerAPIAsync();
            var request = new ListMatchmakingQueuesRequest();
	
            var listResult = await multiplayerApi.ListMatchmakingQueuesAsync(request);
            if (listResult.Error != null)
            {
                return ErrorHandler.ThrowError<ListMatchmakingQueuesResult>(listResult.Error);
            }

            return new ExecuteResult<ListMatchmakingQueuesResult>
            {
                Result = listResult.Result
            };
        }

        public static async Task<ExecuteResult<SetMatchmakingQueueResult>> UpdateMatchmakingQueueAsync(string queueRaw)
        {
            var multiplayerApi = await GetFabMultiplayerAPIAsync();

            var rawData = string.IsNullOrEmpty(queueRaw) ? JsonPlugin.EMPTY_JSON : queueRaw;
            var queue = JsonConvert.DeserializeObject<MatchmakingQueueConfig>(rawData);

            var request = new SetMatchmakingQueueRequest {
                MatchmakingQueue = queue
            };
            
            var setResult = await multiplayerApi.SetMatchmakingQueueAsync(request);
            if (setResult.Error != null)
            {
                return ErrorHandler.ThrowError<SetMatchmakingQueueResult>(setResult.Error);
            }

            return new ExecuteResult<SetMatchmakingQueueResult>
            {
                Result = setResult.Result
            };
        }

        public static async Task<ExecuteResult<RemoveMatchmakingQueueResult>> RemoveMatchmakingQueueAsync(string queueName)
        {
            var multiplayerApi = await GetFabMultiplayerAPIAsync();

            var request = new RemoveMatchmakingQueueRequest {
                QueueName = queueName
            };
            
            var removeResult = await multiplayerApi.RemoveMatchmakingQueueAsync(request);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<RemoveMatchmakingQueueResult>(removeResult.Error);
            }

            return new ExecuteResult<RemoveMatchmakingQueueResult>
            {
                Result = removeResult.Result
            };
        }

        public static async Task<ExecuteResult<GetMatchResult>> GetMatchAsync(string queueName, string matchID)
        {
            var multiplayerApi = await GetFabMultiplayerAPIAsync();

            var matchRequest = new GetMatchRequest
            {
                MatchId = matchID,
                QueueName = queueName,
                ReturnMemberAttributes = true
            };
            var getMatchResult = await multiplayerApi.GetMatchAsync(matchRequest);
            return new ExecuteResult<GetMatchResult>
            {
                Result = getMatchResult.Result
            };
        }
    }
}