using System.Threading.Tasks;
using CBS.Models;
using System.Linq;
using System;

namespace CBS
{
    public class ExpirienceModule : BaseAzureModule
    {
        public static async Task<ExecuteResult<LevelInfo>> GetExpirienceDetailOfEntityAsync(CBSEntityType type, string entityID)
        {    
            var currentExpResult = await GetEntityExpirienceValueAsync(type, entityID);
            if (currentExpResult.Error != null)
            {
                return ErrorHandler.ThrowError<LevelInfo>(currentExpResult.Error);
            }
            var currentExp = currentExpResult.Result.Value;

            var levelDataResult = await GetEntityLevelTableAsync(type);
            if (levelDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<LevelInfo>(levelDataResult.Error);
            }
            var levelData = levelDataResult.Result;
            
            var result = ParseLevelDetail(levelData, currentExp, entityID);
            result.TargetID = entityID;
            result.Type = type;

            return new ExecuteResult<LevelInfo>
            {
                Result = result
            };
        }

        public static async Task<ExecuteResult<FunctionAddExpirienceResult>> AddExpirienceToEntityAsync(CBSEntityType type, string entityID, int expToAdd)
        {
            var entityLevelDetail = await GetExpirienceDetailOfEntityAsync(type, entityID);
            if (entityLevelDetail.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAddExpirienceResult>(entityLevelDetail.Error);
            }

            var prevLevelResult = entityLevelDetail.Result;
            var prevLevel = prevLevelResult.CurrentLevel;
            var oldExpValue = prevLevelResult.CurrentExp;
            
            var levelTableResult = await GetEntityLevelTableAsync(type);
            if (levelTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAddExpirienceResult>(levelTableResult.Error);
            }
            var levelTable = levelTableResult.Result;
            var levelMultiply = levelTable.GetExpMultiply();

            var resultValue = oldExpValue + (int)Math.Floor(expToAdd * levelMultiply);
            
            if (type == CBSEntityType.PLAYER)
            {
                var updateResult = await StatisticModule.UpdateProfileStatisticValueAsync(entityID, StatisticKeys.PlayerExpirienceStatistic, resultValue);
                if (updateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionAddExpirienceResult>(updateResult.Error);
                }
            }
            else if (type == CBSEntityType.CLAN)
            {
                var updateResult = await ClanStatisticModule.UpdateClanStatisticValueAsync(entityID, StatisticKeys.ClanExpirienceStatistic, resultValue);
                if (updateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionAddExpirienceResult>(updateResult.Error);
                }
            }

            var result = ParseLevelDetail(levelTable, resultValue, entityID);
            var updatedLevel = result.CurrentLevel;
            var reachNewLevel = updatedLevel > prevLevel;
            if (reachNewLevel)
            {
                var rewardedObjectResult = await AsignNewLevelResult(type, entityID, levelTable, prevLevel, updatedLevel);
                if (rewardedObjectResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionAddExpirienceResult>(rewardedObjectResult.Error);
                }
                var rewardResult = rewardedObjectResult.Result;
                return new ExecuteResult<FunctionAddExpirienceResult>{
                    Result = result.ToAddExpResult(reachNewLevel, rewardResult)
                };
            }
            return new ExecuteResult<FunctionAddExpirienceResult>{
                Result = result.ToAddExpResult(reachNewLevel, null)
            };
        }

        private static async Task<ExecuteResult<ExpirienceValue>> GetEntityExpirienceValueAsync(CBSEntityType type, string entityID)
        {
            var expValue = 0;

            if (type == CBSEntityType.PLAYER)
            {
                var result = await StatisticModule.GetProfileStatisticValueAsync(entityID, StatisticKeys.PlayerExpirienceStatistic);
                if (result.Error != null)
                {
                    return ErrorHandler.ThrowError<ExpirienceValue>(result.Error);
                }
                expValue = result.Result.Value;
            }
            else if (type == CBSEntityType.CLAN)
            {
                var result = await StatisticModule.GetProfileStatisticValueAsync(entityID, StatisticKeys.ClanExpirienceStatistic);
                if (result.Error != null)
                {
                    return ErrorHandler.ThrowError<ExpirienceValue>(result.Error);
                }
                expValue = result.Result.Value;
            }

            return new ExecuteResult<ExpirienceValue>
            {
                Result = new ExpirienceValue{
                    Value = expValue
                }
            };
        }

        private static async Task<ExecuteResult<LevelTable>> GetEntityLevelTableAsync(CBSEntityType type)
        {
            var levelTable = new LevelTable();
            if (type == CBSEntityType.PLAYER)
            {
                var result = await ProfileExpModule.GetLevelTableAsync();
                if (result.Error != null)
                {
                    return ErrorHandler.ThrowError<LevelTable>(result.Error);
                }
                levelTable = result.Result;
            }
            else if (type == CBSEntityType.CLAN)
            {
                var result = await ClanExpModule.GetLevelTableAsync();
                if (result.Error != null)
                {
                    return ErrorHandler.ThrowError<LevelTable>(result.Error);
                }
                levelTable = result.Result;
            }

            return new ExecuteResult<LevelTable>
            {
                Result = levelTable
            };
        }

        private static async Task<ExecuteResult<GrantRewardResult>> AsignNewLevelResult(CBSEntityType type, string entityID, LevelTable levelTable, int prevLevel, int newLevel)
        {
            var newLevelReward = new RewardObject();
            var newLevelRewardResult = new GrantRewardResult();
            var table = levelTable.Table;
            var levels = table.Skip(prevLevel).Take(newLevel-prevLevel);
            var rewards = levels.Where(x=>x.Reward != null).Select(x=>x.Reward).ToList();
            var rewardsCount = rewards.Count;
            
            if (type == CBSEntityType.PLAYER)
            {
                var profileLevelTable = levelTable as ProfileLevelTable;

                if (profileLevelTable != null)
                {
                    var newLevelEvents = new ProfileEventContainer();
                    var events = profileLevelTable.LevelEvents.Where(x=>x != null).Select(x=>x).ToList();
                    var eventsCount = events.Count;
                    for (int i = 0;i<rewardsCount;i++)
                    {
                        var reward = rewards[i];
                        newLevelReward = newLevelReward.MergeReward(reward);
                    }
                    for (int i = 0;i<eventsCount;i++)
                    {
                        var eventContainer = events[i];
                        newLevelEvents = newLevelEvents.Merge(eventContainer);
                    }

                    var deliveryObject = profileLevelTable.RewardDelivery ?? new RewardDelivery();
                    var devliveryType = deliveryObject.DeliveryType;
                    if (devliveryType == RewardDeliveryType.GRANT_IMMEDIATELY)
                    {
                        // grant reward
                        var grantResult = await RewardModule.GrantRewardToProfileAsync(newLevelReward, entityID);
                        if (grantResult.Error != null)
                        {
                            return ErrorHandler.ThrowError<GrantRewardResult>(grantResult.Error);
                        }
                        newLevelRewardResult = grantResult.Result;
                    }
                    else
                    {
                        // send reward to inbox
                        var notification = CBSNotification.FromRewardDelivery(deliveryObject, newLevelReward);
                        var sendNotificationResult = await NotificationModule.SendNotificationAsync(notification, entityID);
                        if (sendNotificationResult.Error != null)
                        {
                            return ErrorHandler.ThrowError<GrantRewardResult>(sendNotificationResult.Error);
                        }
                    }
                    // execute profile events
                    EventModule.ExecuteProfileEventContainer(entityID, newLevelEvents);
                }
            }
            else if (type == CBSEntityType.CLAN)
            {

            }
            
            return new ExecuteResult<GrantRewardResult>
            {
                Result = newLevelRewardResult
            };
        }

        private static LevelInfo ParseLevelDetail(LevelTable levelData, int currentExp, string targetID)
        {
            int prevLevelExp = 0;
            int nextLevelExp = 0;
            int currentLevel = 0;

            if (levelData.Table != null)
            {
                var objectArray = levelData.Table;
                var experienceArray = objectArray.Select(a => a.Expirience).ToList();
                experienceArray.Sort();
                experienceArray.Reverse();
                prevLevelExp = experienceArray.FirstOrDefault(a => a <= currentExp);
                experienceArray.Reverse();
                
                if (prevLevelExp != 0)
                {
                    currentLevel = experienceArray.IndexOf((int)prevLevelExp) + 1;
                }
                else
                {
                    prevLevelExp = 0;
                }
                
                var nextLevel = currentLevel + 1;
                var nextLevelReward = new RewardObject();
                
                if (nextLevel > experienceArray.Count)
                {
                    nextLevelExp = prevLevelExp;
                }
                else
                {
                    nextLevelExp = experienceArray[(int)nextLevel - 1];
                    nextLevelReward = levelData.Table[(int)nextLevel - 1].Reward;
                }

                var result = new LevelInfo {
                    TargetID = targetID,
                    CurrentLevel = currentLevel,
                    PrevLevelExp = prevLevelExp,
                    NextLevelExp = nextLevelExp,
                    CurrentExp = currentExp,
                    NextLevelReward = nextLevelReward
                };

                return result;
            }
            
            return new LevelInfo();
        }
    }
}