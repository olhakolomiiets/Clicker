using CBS.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CBS
{
    public class TableBattlePassAssistant
    {
        private static readonly string BankPartitionKey = "CBSBank";
        private static readonly string ProfilePartitionKey = "CBSProfile";
        private static readonly string BankTablePrefix = "BP_Bank_";
        private static readonly string CheckInTablePrefix = "BP_Checkin_";

        private static readonly string ExpKey = "Exp";
        private static readonly string RowKey = "RowKey";

        public static async Task<ExecuteResult<Azure.Response>> UpdateBankExpAsync(string profileID, string battlePassInstanceID, int exp)
        {
            var tableID = BankTablePrefix+battlePassInstanceID;
            var profileEntity = GetBankEntity(profileID);
            profileEntity[ExpKey] = exp;
            var updateResult = await CosmosTable.UpdateEntityAsync(tableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> AddBankExpAsync(string profileID, string battlePassInstanceID, int exp)
        {
            var tableID = BankTablePrefix+battlePassInstanceID;
            var profileEntity = GetBankEntity(profileID);
            profileEntity[ExpKey] = exp;
            var addResult = await CosmosTable.AddEntityAsync(tableID, profileEntity);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(addResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = addResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> BattlePassCheckInAsync(string profileID, string battlePassInstanceID)
        {
            var tableID = CheckInTablePrefix+battlePassInstanceID;
            var profileEntity = GetCheckinEntity(profileID);
            var addResult = await CosmosTable.UpsertEntityAsync(tableID, profileEntity);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(addResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = addResult.Result
            };
        }

        public static async Task GrantBankRewardsAsync(string battlePassID, string battlePassInstanceID)
        {
            var tableID = BankTablePrefix+battlePassInstanceID;
            var instanceResult = await BattlePassModule.LoadBattlePassInstnanceByIDAsync(battlePassID);
            var passInstance = instanceResult.Result;
            var stepExp = passInstance.ExpStep;
            var bankLevels = passInstance.GetBankLevels(0, true);
            var rewardDelivery = passInstance.BankRewardDelivery ?? new RewardDelivery();
            var deliveryType = rewardDelivery.DeliveryType;

            var client = CosmosTable.GetTableClient(tableID);
            await client.CreateIfNotExistsAsync();
            var entityResult = client.QueryAsync<Azure.Data.Tables.TableEntity>(select: GetBankEntityKeys());

            string token = null;
            do
            {
                await foreach (var page in entityResult.AsPages(token))
                {
                    var pageList = page.Values;
                    foreach (var qEntity in pageList)
                    {
                        var profileID = qEntity.RowKey;
                        var exp = qEntity.GetInt32(ExpKey);
                        if (exp == null)
                        {
                            exp = 0;
                        }
                        var profileLevel = exp/stepExp;
                        var passedLevels = bankLevels.Where(x=>profileLevel >= x.TargetIndex);
                        var rewards = passedLevels.Select(x=>x.Reward);
                        var bankReward = new RewardObject();
                        foreach (var reward in rewards)
                        {
                            bankReward = bankReward.MergeReward(reward);
                        }
                        if (deliveryType == RewardDeliveryType.GRANT_IMMEDIATELY)
                        {
                            await RewardModule.GrantRewardToProfileAsync(bankReward, profileID);
                        }
                        else
                        {
                            // send reward to inbox
                            var notification = CBSNotification.FromRewardDelivery(rewardDelivery, bankReward);
                            await NotificationModule.SendNotificationAsync(notification, profileID);
                        }
                    }
                    token = page.ContinuationToken;
                }
            }
            while(!string.IsNullOrEmpty(token));
            await CosmosTable.DeleteTableAsync(tableID);
        }

        public static async Task GrantRewardOnEndAsync(string battlePassID, string battlePassInstanceID)
        {
            var tableID = CheckInTablePrefix+battlePassInstanceID;
            var instanceResult = await BattlePassModule.LoadBattlePassInstnanceByIDAsync(battlePassID);
            var passInstance = instanceResult.Result;
            var automaticReward = passInstance.AutomaticRewardOnEnd;
            if (!automaticReward)
                return;
            var rewardDelivery = passInstance.AutomaticRewardDelivery ?? new RewardDelivery();
            var deliveryType = rewardDelivery.DeliveryType;

            var client = CosmosTable.GetTableClient(tableID);
            await client.CreateIfNotExistsAsync();
            var entityResult = client.QueryAsync<Azure.Data.Tables.TableEntity>(select: GetCheckinEntityKeys());

            string token = null;
            do
            {
                await foreach (var page in entityResult.AsPages(token))
                {
                    var pageList = page.Values;
                    foreach (var qEntity in pageList)
                    {
                        var profileID = qEntity.RowKey;
                        var battlePassProfileInfoResult = await BattlePassModule.GetBattlePassFullInformationAsync(profileID, battlePassID, 0, battlePassInstanceID);
                        if (battlePassProfileInfoResult.Error != null)
                        {
                            continue;
                        }
                        var battlePassInfo = battlePassProfileInfoResult.Result ?? new FunctionBattlePassFullInfoResult();
                        var profileState = battlePassInfo.ProfileState;
                        var battlePassState = battlePassInfo.Instance;
                        var extraLevel = profileState.ExtraLevelAccess && battlePassState.ExtraLevelsEnabled;
                        var levelTree = battlePassState.LevelTree ?? new System.Collections.Generic.List<BattlePassLevel>();
                        var profilePassLevel = profileState.PlayerLevel;
                        if (extraLevel)
                        {
                            var extraLevels = battlePassState.ExtraLevelTree ?? new System.Collections.Generic.List<BattlePassLevel>();
                            levelTree.AddRange(extraLevels);
                        }
                        var onEndReward = new RewardObject();
                        for (int i=0;i<levelTree.Count;i++)
                        {
                            var levelDetail = levelTree[i];
                            var levelParser = new  BattlePassLevelInfo(battlePassState, profileState, levelDetail, i, battlePassState.GetMaxLevelCount());
                            levelParser.ForceEnablePass();
                            if (levelParser.IsRewardAvailableToCollect(BattlePassRewardType.DEFAULT))
                            {
                                onEndReward = onEndReward.MergeReward(levelDetail.DefaultReward ?? new RewardObject());
                            }
                            if (levelParser.IsRewardAvailableToCollect(BattlePassRewardType.PREMIUM))
                            {
                                onEndReward = onEndReward.MergeReward(levelDetail.PremiumReward ?? new RewardObject());
                            }
                        }
                        if (onEndReward.IsEmpty())
                            continue;
                        if (deliveryType == RewardDeliveryType.GRANT_IMMEDIATELY)
                        {
                            await RewardModule.GrantRewardToProfileAsync(onEndReward, profileID);
                        }
                        else
                        {
                            // send reward to inbox
                            var notification = CBSNotification.FromRewardDelivery(rewardDelivery, onEndReward);
                            await NotificationModule.SendNotificationAsync(notification, profileID);
                        }
                    }
                    token = page.ContinuationToken;
                }
            }
            while(!string.IsNullOrEmpty(token));
            await CosmosTable.DeleteTableAsync(tableID);
        }

        private static Azure.Data.Tables.TableEntity GetBankEntity(string profileID)
        {
            return new Azure.Data.Tables.TableEntity{
                RowKey = profileID,
                PartitionKey = BankPartitionKey
            };
        }

        private static Azure.Data.Tables.TableEntity GetCheckinEntity(string profileID)
        {
            return new Azure.Data.Tables.TableEntity{
                RowKey = profileID,
                PartitionKey = ProfilePartitionKey
            };
        }

        private static string [] GetBankEntityKeys()
        {
            return new string [] 
            {
                RowKey,
                ExpKey
            };
        }

        private static string [] GetCheckinEntityKeys()
        {
            return new string [] 
            {
                RowKey
            };
        }
    }
}