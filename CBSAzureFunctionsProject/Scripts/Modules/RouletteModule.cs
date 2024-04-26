using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using System;

namespace CBS
{
    public class RouletteModule : BaseAzureModule
    {

        [FunctionName(AzureFunctions.GetRouletteTableMethod)]
        public static async Task<dynamic> GetRouletteTableTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();
            var profileID = request.ProfileID;

            var getResult = await GetRouletteTable();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SpinRouletteMethod)]
        public static async Task<dynamic> SpinRouletteTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();
            var profileID = request.ProfileID;

            var spinResult = await SpinRoulette(profileID);
            if (spinResult.Error != null)
            {
                return ErrorHandler.ThrowError(spinResult.Error).AsFunctionResult();
            }
            return spinResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<RouletteTable>> GetRouletteTable()
        {
            var getTableResult = await GetInternalTitleDataAsObjectAsync<RouletteTable>(TitleKeys.RouletteTitleKey);
            if (getTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<RouletteTable>(getTableResult.Error);
            }
            var table = getTableResult.Result;
            if (table == null || table.Positions == null || table.Positions.Count == 0)
            {
                return ErrorHandler.RouletteNotConfigured<RouletteTable>();
            }

            return new ExecuteResult<RouletteTable>
            {
                Result = table
            };
        }

        public static async Task<ExecuteResult<FunctionSpinRouletteResult>> SpinRoulette(string profileID)
        {
            var getTableResult = await GetRouletteTable();
            if (getTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSpinRouletteResult>(getTableResult.Error);
            }
            var table = getTableResult.Result;
            var positions = table.Positions;
            int poolSize = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                poolSize += positions[i].Weight;
            }
            RoulettePosition droppedPosition = null;
            var rnd = new Random();
            // Get a random integer from 0 to PoolSize.
            int randomNumber = rnd.Next(0, poolSize) + 1;
            // Detect the item, which corresponds to current random number.
            int accumulatedProbability = 0;
            var positionCount = positions.Count;
            for (int i = 0; i < positionCount; i++)
            {
                accumulatedProbability += positions[i].Weight;
                if (randomNumber <= accumulatedProbability)
                {
                    droppedPosition = positions[i];
                    break;
                }
            }

            var reward = droppedPosition.Reward;
            var rewardResult = await RewardModule.GrantRewardToProfileAsync(reward, profileID);
            if (rewardResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSpinRouletteResult>(rewardResult.Error);
            }
            var grantResult = rewardResult.Result;

            var profileEvents = droppedPosition.Events;
            if (profileEvents != null)
            {
                EventModule.ExecuteProfileEventContainer(profileID, profileEvents);
            }

            return new ExecuteResult<FunctionSpinRouletteResult>
            {
                Result = new FunctionSpinRouletteResult
                {
                    RewardResult = grantResult,
                    DroppedPosition = droppedPosition
                }
            };
        }
    }
}