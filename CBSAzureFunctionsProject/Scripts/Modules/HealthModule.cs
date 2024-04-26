using PlayFab.ServerModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using CBS.Models;
using System.Collections.Generic;


namespace CBS
{
    public class HealthModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.CheckHealthMethod)]
        public static async Task<dynamic> CheckHealthTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var getResult = await CheckHealthAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }
            return getResult.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> CheckHealthAsync()
        {
            // check title id
            var playfabTitleID = TitleId;
            if (string.IsNullOrEmpty(playfabTitleID))
            {
                return ErrorHandler.PlayFabTitleIDIsNotConfigured<FunctionEmptyResult>();
            }

            // check secret key
            var secretKey = SercetKey;
            if (string.IsNullOrEmpty(secretKey))
            {
                return ErrorHandler.PlayFabSecretKeyIsNotConfigured<FunctionEmptyResult>();
            }

            // check connection string azure side
            var connectionString = StorageConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                return ErrorHandler.AzureConnectionStringNotConfigured<FunctionEmptyResult>();
            }

            // get titles to check
            var getTitlesRequest = new GetTitleDataRequest
            {
                Keys = new List<string>
                {
                    TitleKeys.FunctionURLKey,
                    TitleKeys.FunctionStorageConnectionStringKey,
                    TitleKeys.FunctionMasterKey
                }
            };

            var getTitleDataResult = await FabServerAPI.GetTitleInternalDataAsync(getTitlesRequest);
            if (getTitleDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(getTitleDataResult.Error);
            }
            var dictionary = getTitleDataResult.Result.Data ?? new Dictionary<string, string>();

            bool storageExist = dictionary.ContainsKey(TitleKeys.FunctionStorageConnectionStringKey);
            var storageConnectionString = storageExist ? dictionary[TitleKeys.FunctionStorageConnectionStringKey] : string.Empty;
            bool masterKeyExist = dictionary.ContainsKey(TitleKeys.FunctionMasterKey);
            var functionMasterKey = masterKeyExist ? dictionary[TitleKeys.FunctionMasterKey] : string.Empty;
            bool functionsURLKeyExist = dictionary.ContainsKey(TitleKeys.FunctionURLKey);
            var functionURL = functionsURLKeyExist ? dictionary[TitleKeys.FunctionURLKey] : string.Empty;

            // check connection string playfab side
            if (string.IsNullOrEmpty(storageConnectionString))
            {
                return ErrorHandler.AzureConnectionStringNotConfigured<FunctionEmptyResult>();
            }

            // check function url
            if (string.IsNullOrEmpty(functionURL))
            {
                return ErrorHandler.FunctionURLNotConfigured<FunctionEmptyResult>();
            }

            // check azure master key
            if (string.IsNullOrEmpty(functionMasterKey))
            {
                return ErrorHandler.AzureKeyNotConfigured<FunctionEmptyResult>();
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }
    }
}