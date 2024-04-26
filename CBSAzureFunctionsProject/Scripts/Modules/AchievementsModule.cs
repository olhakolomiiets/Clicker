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
    public class AchievementsModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.GetProfileAchievementsMethod)]
        public static async Task<dynamic> GetProfileAchievementsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetAchievementsRequest>();
            var profileID = request.ProfileID;
            var query = request.State;

            var getResult = await GetProfileAchievementsAsync(profileID, query);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddAchievementPointsMethod)]
        public static async Task<dynamic> AddAchievementPointsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionModifyAchievementPointsRequest>();
            var profileID = request.ProfileID;
            var points = request.Points;
            var method = request.Method;
            var achievementID = request.AchievementID;

            var modifyResult = await ModifyAchievementsPointsAsync(profileID, achievementID, method, points);
            if (modifyResult.Error != null)
            {
                return ErrorHandler.ThrowError(modifyResult.Error).AsFunctionResult();
            }

            return modifyResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PickupAchievementRewardMethod)]
        public static async Task<dynamic> PickupAchievementRewardTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var achievementID = request.ID;

            var pickupResult = await PickupAchievementRewardAsync(profileID, achievementID);
            if (pickupResult.Error != null)
            {
                return ErrorHandler.ThrowError(pickupResult.Error).AsFunctionResult();
            }

            return pickupResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ResetAchievementMethod)]
        public static async Task<dynamic> ResetAchievementTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var achievementID = request.ID;

            var resetResult = await ResetAchievementAsync(profileID, achievementID);
            if (resetResult.Error != null)
            {
                return ErrorHandler.ThrowError(resetResult.Error).AsFunctionResult();
            }

            return resetResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetAchievementsBadgeMethod)]
        public static async Task<dynamic> GetAchievementsBadgeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();
            var profileID = request.ProfileID;

            var getResult = await GetAchievementsBadgeAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<AchievementsData>> GetAchievementsTableAsync()
        {
            var getTableResult = await GetInternalTitleDataAsObjectAsync<AchievementsData>(TitleKeys.AchievementsTitleKey);
            if (getTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<AchievementsData>(getTableResult.Error);
            }
            var achievementsData = getTableResult.Result;

            return new ExecuteResult<AchievementsData>
            {
                Result = achievementsData
            };
        }

        public static async Task<ExecuteResult<FunctionTasksResult<CBSTask>>> GetProfileAchievementsAsync(string profileID, TasksState queryType)
        {
            var achievementsTasksResult = await TaskModule.GetEntityTasksAsync<CBSTask>(profileID, CBSEntityType.PLAYER, TitleKeys.AchievementsTitleKey, ProfileDataKeys.PlayerAchievements);
            if (achievementsTasksResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionTasksResult<CBSTask>>(achievementsTasksResult.Error);
            }
            var functionResult = achievementsTasksResult.Result;
            var achievementsList = functionResult.Tasks ?? new List<CBSTask>();

            if (queryType == TasksState.ACTIVE)
            {
                achievementsList = achievementsList.Where(x => !x.IsComplete && x.IsAvailable).ToList();
                functionResult.Tasks = achievementsList;
            }
            else if (queryType == TasksState.COMPLETED)
            {
                achievementsList = achievementsList.Where(x => x.IsComplete).ToList();
                functionResult.Tasks = achievementsList;
            }
            achievementsTasksResult.Result = functionResult;

            return achievementsTasksResult;
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<CBSTask>>> ModifyAchievementsPointsAsync(string profileID, string achievementID, ModifyMethod method, int points)
        {
            var modifyRequest = new FunctionModifyTaskPointsRequest
            {
                EntityID = profileID,
                EntityType = CBSEntityType.PLAYER,
                ModifyMethod = method,
                Points = points,
                TaskID = achievementID,
                TasksTitleID = TitleKeys.AchievementsTitleKey,
                TasksEntityTitleID = ProfileDataKeys.PlayerAchievements
            };
            var modifyResult = await TaskModule.ModifyTaskPointAsync<CBSTask>(modifyRequest);
            if (modifyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(modifyResult.Error);
            }
            return modifyResult;
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<CBSTask>>> PickupAchievementRewardAsync(string profileID, string achievementID)
        {
            var pickupResult = await TaskModule.PickupTaskRewardAsync<CBSTask>(profileID, CBSEntityType.PLAYER, TitleKeys.AchievementsTitleKey, ProfileDataKeys.PlayerAchievements, achievementID);
            if (pickupResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(pickupResult.Error);
            }
            return pickupResult;
        }

        public static async Task<ExecuteResult<FunctionModifyTaskResult<CBSTask>>> ResetAchievementAsync(string profileID, string achievementID)
        {
            var resetResult = await TaskModule.ResetTaskAsync<CBSTask>(profileID, CBSEntityType.PLAYER, TitleKeys.AchievementsTitleKey, ProfileDataKeys.PlayerAchievements, achievementID);
            if (resetResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyTaskResult<CBSTask>>(resetResult.Error);
            }
            return resetResult;
        }

        public static async Task<ExecuteResult<FunctionBadgeResult>> GetAchievementsBadgeAsync(string profileID)
        {
            var achievementsResult = await GetProfileAchievementsAsync(profileID, TasksState.ALL);
            if (achievementsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBadgeResult>(achievementsResult.Error);
            }
            var functionResult = achievementsResult.Result;
            var achievementsList = functionResult.Tasks ?? new List<CBSTask>();
            var notRewardedAchievements = achievementsList.Where(x=>x.Type != TaskType.TIERED && x.IsComplete && x.IsRewardAvailable());
            var notRewardedTieredAchievements = achievementsList.Where(x=>x.Type == TaskType.TIERED && x.IsRewardAvailable());
            var notRewardedCount = notRewardedAchievements.Count() + notRewardedTieredAchievements.Count();

            return new ExecuteResult<FunctionBadgeResult>
            {
                Result = new FunctionBadgeResult
                {
                    Count = notRewardedCount
                }
            };
        }
    }
}