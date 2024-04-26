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
using System.Linq;

namespace CBS
{
    public class ClanStatisticModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.GetClanLeaderboardMethod)]
        public static async Task<dynamic> GetClanLeaderboardTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetClanLeaderboardRequest>();

            var getResult = await GetClanLeaderboardAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetLeaderboardAroundClanMethod)]
        public static async Task<dynamic> GetLeaderboardAroundClanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetClanLeaderboardRequest>();

            var getResult = await GetLeaderboardAroundClanAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateClanStatisticMethod)]
        public static async Task<dynamic> UpdateClanStatisticTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateClanStatisticRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var statisticName = request.StatisticName;
            var statisticValue = request.StatisticValue;

            var updateResult = await UpdateClanStatisticValueAsync(clanID, statisticName, statisticValue, profileID);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddClanStatisticMethod)]
        public static async Task<dynamic> AddClanStatisticTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateClanStatisticRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var statisticName = request.StatisticName;
            var statisticValue = request.StatisticValue;

            var addResult = await AddClanStatisticValueAsync(clanID, statisticName, statisticValue, profileID);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError(addResult.Error).AsFunctionResult();
            }

            return addResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionUpdateClanStatisticResult>> UpdateClanStatisticValueAsync(string clanID, string statisticName, int statisticValue, string profileID = null)
        {
            if (!string.IsNullOrEmpty(profileID))
            {
                var hasPermissionResult = await ClanModule.HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.UPDATE_STATISTICS);
                if (hasPermissionResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionUpdateClanStatisticResult>(hasPermissionResult.Error);
                }
                var hasPermission = hasPermissionResult.Result.Value;
                if (!hasPermission)
                {
                    return ErrorHandler.NotEnoughRights<FunctionUpdateClanStatisticResult>();
                }
            }

            var checkClanResult = await ClanModule.CheckIfProfileIsClanAsync(clanID);
            if (checkClanResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateClanStatisticResult>(checkClanResult.Error);
            }
            var isClanProfile = checkClanResult.Result.Value;
            if (!isClanProfile)
            {
                return ErrorHandler.InvalidInput<FunctionUpdateClanStatisticResult>();
            }

            var request = new UpdatePlayerStatisticsRequest {
                PlayFabId = clanID, 
                Statistics = new List<StatisticUpdate>()
                {
                    new StatisticUpdate{
                        StatisticName = statisticName,
                        Value = statisticValue
                    }
                }
            };
            var playerStatResult = await FabServerAPI.UpdatePlayerStatisticsAsync(request);
            if (playerStatResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateClanStatisticResult>(playerStatResult.Error);
            }
            // update with azure table
            var getRequest = new FunctionGetClanLeaderboardRequest
            {
                ClanID = clanID,
                StatisticName = statisticName,
                MaxCount = 1
            };
            var getResult = await GetLeaderboardAroundClanAsync(getRequest);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateClanStatisticResult>(getResult.Error);
            }
            var leaderboardResult = getResult.Result;
            var userResult = leaderboardResult.Leaderboard.FirstOrDefault();
            var stats = userResult.StatisticValue;
            var position = userResult.StatisticPosition;
            var tableResult = await TableClanAssistant.UpdateClanStatisticAsync(clanID, statisticName, stats, position);
            if (tableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateClanStatisticResult>(tableResult.Error);
            }

            return new ExecuteResult<FunctionUpdateClanStatisticResult>
            {
                Result = new FunctionUpdateClanStatisticResult
                {
                    ClanID = clanID,
                    StatisticName = statisticName,
                    StatisticPosition = position,
                    StatisticValue = stats
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetClanLeaderboardResult>> GetClanLeaderboardAsync(FunctionGetClanLeaderboardRequest request)
        {
            var clanID = request.ClanID;
            var statisticName = request.StatisticName;
            var maxClans = request.MaxCount;
            var version = request.Version;
            var constraints = request.Constraints;
            var startPostion = request.StartPostion;

            var leaderboardRequest = new GetLeaderboardRequest
            {
                StartPosition = startPostion,
                StatisticName = statisticName,
                MaxResultsCount = maxClans,
                Version = version
            };
            var leaderboardResult = await FabServerAPI.GetLeaderboardAsync(leaderboardRequest);
            if (leaderboardResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanLeaderboardResult>(leaderboardResult.Error);
            }
            var fabLeaderboard = leaderboardResult.Result;
            var clanEntries = fabLeaderboard.Leaderboard ?? new List<PlayFab.ServerModels.PlayerLeaderboardEntry>();
            var nextResetDate = fabLeaderboard.NextReset;
            var leaderboardVersion = fabLeaderboard.Version;
            var clanIDs = clanEntries.Select(x=>x.PlayFabId).ToArray();

            var clanDetailsResult = await TableClanAssistant.GetClansDetailsAsync(clanIDs, constraints);
            if (clanDetailsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanLeaderboardResult>(clanDetailsResult.Error);
            }
            var clans = clanDetailsResult.Result;
            var cbsLeaderboard = new List<ClanLeaderboardEntry>();

            for (int i = 0; i < clanEntries.Count; i++)
            {
                var fabEntry = clanEntries[i];
                var entryID = fabEntry.PlayFabId;
                var clanInfo = clans.ContainsKey(entryID) ? clans[entryID] : null;
                var entryPosition = fabEntry.Position;
                var entryValue = fabEntry.StatValue;
                var cbsEntry = ClanLeaderboardEntry.FromClanEntity(clanInfo, entryPosition, entryValue);
                cbsLeaderboard.Add(cbsEntry);
            }

            var clanEntry = cbsLeaderboard.FirstOrDefault(x=>x.ClanID == clanID);

            if (clanEntry == null && !string.IsNullOrEmpty(clanID))
            {
                var getAroundRequest = request;
                getAroundRequest.MaxCount = 1;
                var getAroundResult = await GetLeaderboardAroundClanAsync(getAroundRequest);
                if (getAroundResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGetClanLeaderboardResult>(getAroundResult.Error);
                }
                var aroundResult = getAroundResult.Result;
                clanEntry = aroundResult.ClanEntry;
            }

            return new ExecuteResult<FunctionGetClanLeaderboardResult>
            {
                Result = new FunctionGetClanLeaderboardResult
                {
                    NextReset = nextResetDate,
                    Version = leaderboardVersion,
                    Leaderboard = cbsLeaderboard,
                    ClanEntry = clanEntry
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetClanLeaderboardResult>> GetLeaderboardAroundClanAsync(FunctionGetClanLeaderboardRequest request)
        {
            var clanID = request.ClanID;
            var statisticName = request.StatisticName;
            var maxClans = request.MaxCount;
            var version = request.Version;
            var constraints = request.Constraints;

            var leaderboardRequest = new GetLeaderboardAroundUserRequest
            {
                PlayFabId = clanID,
                StatisticName = statisticName,
                MaxResultsCount = maxClans,
                Version = version
            };
            var leaderboardResult = await FabServerAPI.GetLeaderboardAroundUserAsync(leaderboardRequest);
            if (leaderboardResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanLeaderboardResult>(leaderboardResult.Error);
            }
            var fabLeaderboard = leaderboardResult.Result;
            var clanEntries = fabLeaderboard.Leaderboard ?? new List<PlayFab.ServerModels.PlayerLeaderboardEntry>();
            var nextResetDate = fabLeaderboard.NextReset;
            var leaderboardVersion = fabLeaderboard.Version;
            var clanIDs = clanEntries.Select(x=>x.PlayFabId).ToArray();

            var clanDetailsResult = await TableClanAssistant.GetClansDetailsAsync(clanIDs, constraints);
            if (clanDetailsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanLeaderboardResult>(clanDetailsResult.Error);
            }
            var clans = clanDetailsResult.Result;
            var cbsLeaderboard = new List<ClanLeaderboardEntry>();

            for (int i = 0; i < clanEntries.Count; i++)
            {
                var fabEntry = clanEntries[i];
                var entryID = fabEntry.PlayFabId;
                var clanInfo = clans.ContainsKey(entryID) ? clans[entryID] : null;
                var entryPosition = fabEntry.Position;
                var entryValue = fabEntry.StatValue;
                var cbsEntry = ClanLeaderboardEntry.FromClanEntity(clanInfo, entryPosition, entryValue);
                cbsLeaderboard.Add(cbsEntry);
            }

            var clanEntry = cbsLeaderboard.FirstOrDefault(x=>x.ClanID == clanID);

            return new ExecuteResult<FunctionGetClanLeaderboardResult>
            {
                Result = new FunctionGetClanLeaderboardResult
                {
                    NextReset = nextResetDate,
                    Version = leaderboardVersion,
                    Leaderboard = cbsLeaderboard,
                    ClanEntry = clanEntry
                }
            };
        }

        public static async Task<ExecuteResult<FunctionUpdateClanStatisticResult>> AddClanStatisticValueAsync(string clanID, string statisticName, int statisticValue, string profileID = null)
        {
            var getStatResult = await GetClanStatisticValueAsync(clanID, statisticName);
            if (getStatResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateClanStatisticResult>(getStatResult.Error);
            }
            var statResult = getStatResult.Result;
            var statValue = statResult.Value;
            var newStatValue = statValue + statisticValue;

            var updateResult = await UpdateClanStatisticValueAsync(clanID, statisticName, newStatValue, profileID);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateClanStatisticResult>(updateResult.Error);
            }

            return new ExecuteResult<FunctionUpdateClanStatisticResult>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<StatisticValue>> GetClanStatisticValueAsync(string clanID, string statisticName)
        {
            var request = new GetPlayerStatisticsRequest {
                PlayFabId = clanID,
                StatisticNames = new List<string>() {statisticName}
            };

            var result = await FabServerAPI.GetPlayerStatisticsAsync(request);

            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<StatisticValue>(result.Error);
            }

            if (result.Result.Statistics.Count > 0)
            {
                var experienceStats = result.Result.Statistics.FirstOrDefault();
                return new ExecuteResult<StatisticValue>
                {
                    Result = experienceStats
                };
            }
            else
            {
                return new ExecuteResult<StatisticValue>
                {
                    Result = new StatisticValue
                    {
                        Value = 0
                    }
                };
            }
        }
    }
}