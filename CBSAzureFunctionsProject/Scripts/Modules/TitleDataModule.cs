using PlayFab.ServerModels;
using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using System.Collections.Generic;

namespace CBS
{
    public class TitleDataModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.GetTitleDataByKeyMethod)]
        public static async Task<dynamic> GetTitleDataByKeyTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionKeyRequest>();
            var dataKey = request.Key;

            var getResult = await GetTitleDataByKeyAsync(dataKey);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetAllTitleDataMethod)]
        public static async Task<dynamic> GetAllTitleDataTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            var getResult = await GetAllTitleDataAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionTitleDataByKeyResult>> GetTitleDataByKeyAsync(string key)
        {
            var keysToLoad = new List<string> {key};
            var request = new GetTitleDataRequest
            {
                Keys = keysToLoad
            };
            var titleResult = await FabServerAPI.GetTitleDataAsync(request);
            if (titleResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionTitleDataByKeyResult>(titleResult.Error);
            }
            var data = titleResult.Result.Data;
            if (!data.ContainsKey(key))
            {
                return ErrorHandler.TicketNotFound<FunctionTitleDataByKeyResult>();
            }
            var titleKey = key;
            var titleRawData = data[key];

            return new ExecuteResult<FunctionTitleDataByKeyResult>
            {
                Result = new FunctionTitleDataByKeyResult
                {
                    Key = titleKey,
                    RawData = titleRawData
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetAllTitleDataResult>> GetAllTitleDataAsync()
        {
            var request = new GetTitleDataRequest();
            var titleResult = await FabServerAPI.GetTitleDataAsync(request);
            if (titleResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetAllTitleDataResult>(titleResult.Error);
            }
            var data = titleResult.Result.Data;

            return new ExecuteResult<FunctionGetAllTitleDataResult>
            {
                Result = new FunctionGetAllTitleDataResult
                {
                    Data = data
                }
            };
        }
    }
}