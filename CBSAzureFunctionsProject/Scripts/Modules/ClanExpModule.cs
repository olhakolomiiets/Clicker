using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using Medallion.Threading.Azure;
using Medallion.Threading;

namespace CBS
{
    public class ClanExpModule : BaseAzureModule
    {
        private static readonly string LockIDPrefix = "clanexp"; 

        [FunctionName(AzureFunctions.AddExperienceToClanMethod)]
        public static async Task<dynamic> AddExperienceToClanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionAddClanExpRequest>();

            var expToAdd = request.ExpToAdd;
            var clanID = request.ClanID;

            var addResult = await AddExpirienceToClanAsync(clanID, expToAdd);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError(addResult.Error).AsFunctionResult();
            }

            return addResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetClanExperienceMethod)]
        public static async Task<dynamic> GetClanExpirienceDetailTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanRequest>();

            var clanID = request.ClanID;

            var getResult = await GetClanExpirienceDetailAsync(clanID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<ClanLevelTable>> GetLevelTableAsync()
        {
            var result = await ClanModule.GetClanMetaDataAsync();
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<ClanLevelTable>(result.Error);
            }
            var table = result.Result.GetLevelTable();
            if (table == null)
            {
                return ErrorHandler.LevelTableNotConfiguratedError<ClanLevelTable>();
            }
            return new ExecuteResult<ClanLevelTable>{
                Result = table
            };
        }

        public static async Task<ExecuteResult<LevelInfo>> GetClanExpirienceDetailAsync(string clanID)
        {
            var expResult = await ExpirienceModule.GetExpirienceDetailOfEntityAsync(CBSEntityType.CLAN, clanID);
            if (expResult.Error != null)
            {
                return ErrorHandler.ThrowError<LevelInfo>(expResult.Error);
            }
            var levelDetail = expResult.Result;
            return new ExecuteResult<LevelInfo>
            {
                Result = levelDetail
            };
        }

        public static async Task<ExecuteResult<FunctionAddExpirienceResult>> AddExpirienceToClanAsync(string clanID, int expToAdd)
        {
            // distributed lock by entity id
            var lockID = LockIDPrefix + clanID;
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(lockID))
            {
                await using var handle = await locker.TryAcquireLockAsync(lockID);  
                var checkClanResult = await ClanModule.CheckIfProfileIsClanAsync(clanID);
                if (checkClanResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionAddExpirienceResult>(checkClanResult.Error);
                }
                var isClanProfile = checkClanResult.Result.Value;
                if (!isClanProfile)
                {
                    return ErrorHandler.InvalidInput<FunctionAddExpirienceResult>();
                }

                var addResult = await ExpirienceModule.AddExpirienceToEntityAsync(CBSEntityType.CLAN, clanID, expToAdd);
                if (addResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionAddExpirienceResult>(addResult.Error);
                }
                var levelDetail = addResult.Result;
                var level = levelDetail.CurrentLevel;
                var expirience = levelDetail.CurrentExp;

                var tableUpdateResult = await TableClanAssistant.UpdateClanExpirienceAsync(clanID, level, expirience);
                if (tableUpdateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionAddExpirienceResult>(tableUpdateResult.Error);
                }
                return new ExecuteResult<FunctionAddExpirienceResult>
                {
                    Result = new FunctionAddExpirienceResult()
                };
            }   
        }       
    }
}