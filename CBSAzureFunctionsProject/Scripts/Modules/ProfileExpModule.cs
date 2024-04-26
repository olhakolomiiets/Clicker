using System.Linq;
using System.Threading.Tasks;
using CBS.Models;

namespace CBS
{
    public class ProfileExpModule : BaseAzureModule
    {
        public static async Task<ExecuteResult<ProfileLevelTable>> GetLevelTableAsync()
        {
            var titleKey = TitleKeys.LevelTitleDataKey;
            var result = await GetInternalTitleDataAsObjectAsync<ProfileLevelTable>(titleKey);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<ProfileLevelTable>(result.Error);
            }
            var table = result.Result;
            if (table == null)
            {
                return ErrorHandler.LevelTableNotConfiguratedError<ProfileLevelTable>();
            }
            return new ExecuteResult<ProfileLevelTable>{
                Result = result.Result
            };
        }

        public static async Task<ExecuteResult<LevelInfo>> GetProfileExpirienceDetailAsync(string profileID)
        {
            var expResult = await ExpirienceModule.GetExpirienceDetailOfEntityAsync(CBSEntityType.PLAYER, profileID);
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

        public static async Task<ExecuteResult<FunctionAddExpirienceResult>> AddExpirienceToPlayerAsync(string profileID, int expToAdd)
        {
            var addResult = await ExpirienceModule.AddExpirienceToEntityAsync(CBSEntityType.PLAYER, profileID, expToAdd);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAddExpirienceResult>(addResult.Error);
            }
            var levelDetail = addResult.Result;
            var level = levelDetail.CurrentLevel;
            var expirience = levelDetail.CurrentExp;
            var tableUpdateResult = await TableProfileAssistant.UpdateProfileExpirienceAsync(profileID, level, expirience);
            if (tableUpdateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAddExpirienceResult>(tableUpdateResult.Error);
            }
            return new ExecuteResult<FunctionAddExpirienceResult>
            {
                Result = levelDetail
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> UpdateExpMultiplierAsync(float expMultipliyer)
        {
            var levelTableResult = await GetLevelTableAsync();
            if (levelTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(levelTableResult.Error);
            }
            var levelTable = levelTableResult.Result;
            levelTable.ExpMultiply = expMultipliyer;

            var saveResult = await SaveLevelTableAsync(levelTable);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SaveLevelTableAsync(LevelTable levelTable)
        {
            var rawData = JsonPlugin.ToJson(levelTable);
            var saveResult = await SaveInternalTitleDataAsync(TitleKeys.LevelTitleDataKey, rawData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> AddExpFromStatisitcValueAsync(string profileID, string statisticName)
        {
            var leaderboardRequest = new FunctionGetLeaderboardRequest
            {
                ProfileID = profileID,
                MaxCount = 1,
                StatisticName = statisticName
            };
            var statisticResult = await StatisticModule.GetLeaderboardAroundProfileAsync(leaderboardRequest);
            if (statisticResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(statisticResult.Error);
            }
            var statisticValue = statisticResult.Result.Leaderboard.FirstOrDefault()?.StatisticValue;

            var addExpResult = await AddExpirienceToPlayerAsync(profileID, statisticValue.GetValueOrDefault());
            if (addExpResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(addExpResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }
    }
}