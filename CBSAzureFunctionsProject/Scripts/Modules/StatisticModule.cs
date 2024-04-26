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
using PlayFab.AdminModels;

namespace CBS
{
    public class StatisticModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.GetLeaderboardMethod)]
        public static async Task<dynamic> GetLeaderboardTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetLeaderboardRequest>();

            var getResult = await GetLeaderboardAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetLeaderboardAroundProfileMethod)]
        public static async Task<dynamic> GetLeaderboardAroundProfileTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetLeaderboardRequest>();

            var getResult = await GetLeaderboardAroundProfileAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetFriendsLeaderboardMethod)]
        public static async Task<dynamic> GetFriendsLeaderboardTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetLeaderboardRequest>();

            var getResult = await GetFriendsLeaderboardAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateStatisticMethod)]
        public static async Task<dynamic> UpdateStatisticTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateStatisticRequest>();
            var profileID = request.ProfileID;
            var statisticName = request.StatisticName;
            var statisticValue = request.StatisticValue;

            var updateResult = await UpdateProfileStatisticValueAsync(profileID, statisticName, statisticValue);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddStatisticMethod)]
        public static async Task<dynamic> AddStatisticTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateStatisticRequest>();
            var profileID = request.ProfileID;
            var statisticName = request.StatisticName;
            var statisticValue = request.StatisticValue;

            var addResult = await AddProfileStatisticValueAsync(profileID, statisticName, statisticValue);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError(addResult.Error).AsFunctionResult();
            }

            return addResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ResetProfileStatisticsMethod)]
        public static async Task<dynamic> ResetProfileStatisticsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();
            var profileID = request.ProfileID;

            var resetResult = await ResetProfileStatisticsAsync(profileID);
            if (resetResult.Error != null)
            {
                return ErrorHandler.ThrowError(resetResult.Error).AsFunctionResult();
            }

            return resetResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<StatisticValue>> GetProfileStatisticValueAsync(string profileID, string statisticName)
        {
            var request = new GetPlayerStatisticsRequest {
                PlayFabId = profileID,
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

        public static async Task<ExecuteResult<FunctionUpdateStatisticResult>> AddProfileStatisticValueAsync(string profileID, string statisticName, int statisticValue)
        {
            var getStatResult = await GetProfileStatisticValueAsync(profileID, statisticName);
            if (getStatResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateStatisticResult>(getStatResult.Error);
            }
            var statResult = getStatResult.Result;
            var statValue = statResult.Value;
            var newStatValue = statValue + statisticValue;

            var updateResult = await UpdateProfileStatisticValueAsync(profileID, statisticName, newStatValue);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateStatisticResult>(updateResult.Error);
            }

            return new ExecuteResult<FunctionUpdateStatisticResult>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<FunctionUpdateStatisticResult>> UpdateProfileStatisticValueAsync(string profileID, string statisticName, int statisticValue)
        {
            var request = new UpdatePlayerStatisticsRequest {
                PlayFabId = profileID, 
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
                return ErrorHandler.ThrowError<FunctionUpdateStatisticResult>(playerStatResult.Error);
            }
            // update with azure table
            var getRequest = new FunctionGetLeaderboardRequest
            {
                ProfileID = profileID,
                StatisticName = statisticName,
                MaxCount = 1
            };
            var getResult = await GetLeaderboardAroundProfileAsync(getRequest);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateStatisticResult>(getResult.Error);
            }
            var leaderboardResult = getResult.Result;
            var userResult = leaderboardResult.Leaderboard.FirstOrDefault();
            var stats = userResult.StatisticValue;
            var position = userResult.StatisticPosition;
            var tableResult = await TableProfileAssistant.UpdateProfileStatisticAsync(profileID, statisticName, stats, position);
            if (tableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateStatisticResult>(tableResult.Error);
            }

            return new ExecuteResult<FunctionUpdateStatisticResult>
            {
                Result = new FunctionUpdateStatisticResult
                {
                    ProfileID = profileID,
                    StatisticName = statisticName,
                    StatisticPosition = position,
                    StatisticValue = stats
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetLeaderboardResult>> GetLeaderboardAroundProfileAsync(FunctionGetLeaderboardRequest request)
        {
            var profileID = request.ProfileID;
            var statisticName = request.StatisticName;
            var maxPlayers = request.MaxCount;
            var version = request.Version;
            var constraints = request.Constraints;

            var leaderboardRequest = new GetLeaderboardAroundUserRequest
            {
                PlayFabId = profileID,
                StatisticName = statisticName,
                MaxResultsCount = maxPlayers,
                Version = version
            };
            var leaderboardResult = await FabServerAPI.GetLeaderboardAroundUserAsync(leaderboardRequest);
            if (leaderboardResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetLeaderboardResult>(leaderboardResult.Error);
            }
            var fabLeaderboard = leaderboardResult.Result;
            var playerEntries = fabLeaderboard.Leaderboard ?? new List<PlayFab.ServerModels.PlayerLeaderboardEntry>();
            var nextResetDate = fabLeaderboard.NextReset;
            var leaderboardVersion = fabLeaderboard.Version;
            var profileIDs = playerEntries.Select(x=>x.PlayFabId).ToArray();

            var profileDetailsResult = await TableProfileAssistant.GetProfilesDetailsAsync(profileIDs, constraints);
            if (profileDetailsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetLeaderboardResult>(profileDetailsResult.Error);
            }
            var profiles = profileDetailsResult.Result;
            var cbsLeaderboard = new List<ProfileLeaderboardEntry>();

            for (int i = 0; i < playerEntries.Count; i++)
            {
                var fabEntry = playerEntries[i];
                var entryID = fabEntry.PlayFabId;
                var profileInfo = profiles.ContainsKey(entryID) ? profiles[entryID] : null;
                var entryPosition = fabEntry.Position;
                var entryValue = fabEntry.StatValue;
                var cbsEntry = ProfileLeaderboardEntry.FromProfileEntity(profileInfo, entryPosition, entryValue);
                cbsLeaderboard.Add(cbsEntry);
            }

            var profileEntry = cbsLeaderboard.FirstOrDefault(x=>x.ProfileID == profileID);

            return new ExecuteResult<FunctionGetLeaderboardResult>
            {
                Result = new FunctionGetLeaderboardResult
                {
                    NextReset = nextResetDate,
                    Version = leaderboardVersion,
                    Leaderboard = cbsLeaderboard,
                    ProfileEntry = profileEntry
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetLeaderboardResult>> GetLeaderboardAsync(FunctionGetLeaderboardRequest request)
        {
            var profileID = request.ProfileID;
            var statisticName = request.StatisticName;
            var maxPlayers = request.MaxCount;
            var version = request.Version;
            var constraints = request.Constraints;
            var startPostion = request.StartPostion;

            var leaderboardRequest = new GetLeaderboardRequest
            {
                StartPosition = startPostion,
                StatisticName = statisticName,
                MaxResultsCount = maxPlayers,
                Version = version
            };
            var leaderboardResult = await FabServerAPI.GetLeaderboardAsync(leaderboardRequest);
            if (leaderboardResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetLeaderboardResult>(leaderboardResult.Error);
            }
            var fabLeaderboard = leaderboardResult.Result;
            var playerEntries = fabLeaderboard.Leaderboard ?? new List<PlayFab.ServerModels.PlayerLeaderboardEntry>();
            var nextResetDate = fabLeaderboard.NextReset;
            var leaderboardVersion = fabLeaderboard.Version;
            var profileIDs = playerEntries.Select(x=>x.PlayFabId).ToArray();

            var profileDetailsResult = await TableProfileAssistant.GetProfilesDetailsAsync(profileIDs, constraints);
            if (profileDetailsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetLeaderboardResult>(profileDetailsResult.Error);
            }
            var profiles = profileDetailsResult.Result;
            var cbsLeaderboard = new List<ProfileLeaderboardEntry>();

            for (int i = 0; i < playerEntries.Count; i++)
            {
                var fabEntry = playerEntries[i];
                var entryID = fabEntry.PlayFabId;
                var profileInfo = profiles.ContainsKey(entryID) ? profiles[entryID] : null;
                var entryPosition = fabEntry.Position;
                var entryValue = fabEntry.StatValue;
                var cbsEntry = ProfileLeaderboardEntry.FromProfileEntity(profileInfo, entryPosition, entryValue);
                cbsLeaderboard.Add(cbsEntry);
            }

            var profileEntry = cbsLeaderboard.FirstOrDefault(x=>x.ProfileID == profileID);

            if (profileEntry == null)
            {
                var getAroundRequest = request;
                getAroundRequest.MaxCount = 1;
                var getAroundResult = await GetLeaderboardAroundProfileAsync(getAroundRequest);
                if (getAroundResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGetLeaderboardResult>(getAroundResult.Error);
                }
                var aroundResult = getAroundResult.Result;
                profileEntry = aroundResult.ProfileEntry;
            }

            return new ExecuteResult<FunctionGetLeaderboardResult>
            {
                Result = new FunctionGetLeaderboardResult
                {
                    NextReset = nextResetDate,
                    Version = leaderboardVersion,
                    Leaderboard = cbsLeaderboard,
                    ProfileEntry = profileEntry
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetLeaderboardResult>> GetFriendsLeaderboardAsync(FunctionGetLeaderboardRequest request)
        {
            var profileID = request.ProfileID;
            var statisticName = request.StatisticName;
            var maxPlayers = request.MaxCount;
            var version = request.Version;
            var constraints = request.Constraints;
            var startPostion = request.StartPostion;

            var leaderboardRequest = new GetFriendLeaderboardRequest
            {
                PlayFabId = profileID,
                StartPosition = startPostion,
                StatisticName = statisticName,
                MaxResultsCount = maxPlayers,
                Version = version
            };
            var leaderboardResult = await FabServerAPI.GetFriendLeaderboardAsync(leaderboardRequest);
            if (leaderboardResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetLeaderboardResult>(leaderboardResult.Error);
            }
            var fabLeaderboard = leaderboardResult.Result;
            var playerEntries = fabLeaderboard.Leaderboard ?? new List<PlayFab.ServerModels.PlayerLeaderboardEntry>();
            var nextResetDate = fabLeaderboard.NextReset;
            var leaderboardVersion = fabLeaderboard.Version;
            var profileIDs = playerEntries.Select(x=>x.PlayFabId).ToArray();

            var profileDetailsResult = await TableProfileAssistant.GetProfilesDetailsAsync(profileIDs, constraints);
            if (profileDetailsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetLeaderboardResult>(profileDetailsResult.Error);
            }
            var profiles = profileDetailsResult.Result;
            var cbsLeaderboard = new List<ProfileLeaderboardEntry>();

            for (int i = 0; i < playerEntries.Count; i++)
            {
                var fabEntry = playerEntries[i];
                var entryID = fabEntry.PlayFabId;
                var profileInfo = profiles.ContainsKey(entryID) ? profiles[entryID] : null;
                var entryPosition = fabEntry.Position;
                var entryValue = fabEntry.StatValue;
                var cbsEntry = ProfileLeaderboardEntry.FromProfileEntity(profileInfo, entryPosition, entryValue);
                cbsLeaderboard.Add(cbsEntry);
            }

            var profileEntry = cbsLeaderboard.FirstOrDefault(x=>x.ProfileID == profileID);

            if (profileEntry == null)
            {
                var getAroundRequest = request;
                getAroundRequest.MaxCount = 1;
                var getAroundResult = await GetLeaderboardAroundProfileAsync(getAroundRequest);
                if (getAroundResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGetLeaderboardResult>(getAroundResult.Error);
                }
                var aroundResult = getAroundResult.Result;
                profileEntry = aroundResult.ProfileEntry;
            }

            return new ExecuteResult<FunctionGetLeaderboardResult>
            {
                Result = new FunctionGetLeaderboardResult
                {
                    NextReset = nextResetDate,
                    Version = leaderboardVersion,
                    Leaderboard = cbsLeaderboard,
                    ProfileEntry = profileEntry
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ResetProfileStatisticsAsync(string profileID)
        {
            var adminApi = await GetFabAdminAPIAsync();

            var resetRequest = new PlayFab.AdminModels.ResetUserStatisticsRequest{
                PlayFabId = profileID
            };
            var resetResult = await adminApi.ResetUserStatisticsAsync(resetRequest);
            if (resetResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(resetResult.Error);
            }

            var resetLevelResult = await TableProfileAssistant.UpdateProfileExpirienceAsync(profileID, 0, 0);
            if (resetLevelResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(resetLevelResult.Error);
            }

            var resetStatisticsResult = await TableProfileAssistant.ResetProfileStatisticAsync(profileID);
            if (resetStatisticsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(resetStatisticsResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ResetStatisticsAsync(string statisitcName)
        {
            var adminApi = await GetFabAdminAPIAsync();

            var request = new IncrementPlayerStatisticVersionRequest { 
                StatisticName = statisitcName
            };

            var resetResult = await adminApi.IncrementPlayerStatisticVersionAsync(request);
            if (resetResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(resetResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }
    }
}