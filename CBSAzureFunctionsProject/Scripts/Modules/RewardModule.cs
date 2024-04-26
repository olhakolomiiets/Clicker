using PlayFab.ServerModels;
using System.Threading.Tasks;
using CBS.Models;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class RewardModule : BaseAzureModule
    {
        private static readonly string ItemsCatalogID = CatalogKeys.ItemsCatalogID;

        public static async Task<ExecuteResult<RewardObject>> GrantRegistrationRewardAsync(string profileID)
        {
            var levelResult = await ProfileExpModule.GetLevelTableAsync();
            if (levelResult.Error == null)
            {
                var levelData = levelResult.Result;
                if (levelData != null)
                {
                    // grant reward
                    var rewardObject = levelData.RegistrationPrize;
                    var grantResult = await GrantRewardToProfileAsync(rewardObject, profileID);
                    // execute events
                    var profileEvent = levelData.RegistrationEvents;
                    EventModule.ExecuteProfileEventContainer(profileID, profileEvent);
                    return new ExecuteResult<RewardObject>{
                        Result = rewardObject
                    };
                }
            }
            return new ExecuteResult<RewardObject>{};
        }

        public static async Task<ExecuteResult<GrantRewardResult>> GrantRewardToProfileAsync(RewardObject rewardObject, string profileID)
        {
            if (rewardObject != null)
            {
                var grantedInstances = new List<PlayFab.ClientModels.ItemInstance>();
                // grant items
                var items = rewardObject.BundledItems;
                if (items != null && items.Count > 0)
                {
                    var grantResult = await InternalGrantItemsToPlayerAsync(ItemsCatalogID, items, profileID);
                    if (grantResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<GrantRewardResult>(grantResult.Error);
                    }
                    var instances = grantResult.Result.ItemGrantResults ?? new List<GrantedItemInstance>();
                    var clientInstances = instances.ToClientInstances();
                    grantedInstances = grantedInstances.Concat(clientInstances).ToList();
                }
                // grant lutboxes
                var lutboxes = rewardObject.Lootboxes;
                if (lutboxes != null && lutboxes.Count > 0)
                {
                    var grantResult = await InternalGrantItemsToPlayerAsync(ItemsCatalogID, lutboxes, profileID);
                    if (grantResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<GrantRewardResult>(grantResult.Error);
                    }
                    var instances = grantResult.Result.ItemGrantResults ?? new List<GrantedItemInstance>();
                    var clientInstances = instances.ToClientInstances();
                    grantedInstances = grantedInstances.Concat(clientInstances).ToList();
                }
                // grant currenices
                var currencies = rewardObject.BundledVirtualCurrencies;
                if (currencies != null && currencies.Keys.Count > 0)
                {
                    foreach (var currency in currencies)
                    {
                        var code = currency.Key;
                        var val = currency.Value;
                        await FabServerAPI.AddUserVirtualCurrencyAsync(new AddUserVirtualCurrencyRequest { 
                            PlayFabId = profileID, 
                            VirtualCurrency = code, 
                            Amount = (int)val 
                        });
                    }
                }
                // grant exp
                var hasExp = rewardObject.AddExpirience;
                var expVal = rewardObject.ExpirienceValue;
                if (hasExp && expVal > 0)
                {
                    var addExpResult = await ProfileExpModule.AddExpirienceToPlayerAsync(profileID, expVal);
                    if (addExpResult.Error == null)
                    {
                        var levelInfo = addExpResult.Result ?? new FunctionAddExpirienceResult();
                        if (levelInfo.NewLevelReached)
                        {
                            var levelReward = levelInfo.NewLevelReward ?? new GrantRewardResult();
                            var grantedObject = levelReward.OriginReward ?? new RewardObject();
                            var grantedLevelInstances = levelReward.GrantedInstances ?? new List<PlayFab.ClientModels.ItemInstance>();
                            var grantedLevelCurrencies = levelReward.GrantedCurrencies ?? new Dictionary<string, uint>();
                            currencies = currencies.Concat(grantedLevelCurrencies).ToDictionary(x=>x.Key, x=>x.Value);
                            grantedInstances = grantedInstances.Concat(grantedLevelInstances).ToList();
                            rewardObject.MergeReward(grantedObject);
                        }
                    } 
                }
                return new ExecuteResult<GrantRewardResult>{
                    Result = new GrantRewardResult
                    {
                        OriginReward = rewardObject,
                        GrantedCurrencies = currencies,
                        GrantedInstances = grantedInstances
                    }
                };
            }
            return new ExecuteResult<GrantRewardResult>();
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> RewardAllMembersOfClanAsync(string clanID, RewardObject reward)
        {
            var clanMembersResult = await ClanModule.GetClanMembersAsync(clanID, new CBSProfileConstraints());
            if (clanMembersResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(clanMembersResult.Error);
            }

            var members = clanMembersResult.Result.Members ?? new List<ClanMember>();
            var rewardTaskList = new List<Task<ExecuteResult<GrantRewardResult>>>();
            foreach (var member in members)
            {
                rewardTaskList.Add(GrantRewardToProfileAsync(reward, member.ProfileID));
            }
            await Task.WhenAll(rewardTaskList);

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> RewardAllMembersOfTopClansAsync(string statisticName, int nTop, RewardObject reward)
        {
            var learderboardRequest = new FunctionGetClanLeaderboardRequest
            {
                StatisticName = statisticName,
                Constraints = new CBSClanConstraints(),
                MaxCount = nTop
            };
            var clansLeaderboadResult = await ClanStatisticModule.GetClanLeaderboardAsync(learderboardRequest);
            if (clansLeaderboadResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(clansLeaderboadResult.Error);
            }
            var clanList = clansLeaderboadResult.Result.Leaderboard;
            var taskList = new List<Task<ExecuteResult<FunctionEmptyResult>>>();
            foreach (var clan in clanList)
            {
                taskList.Add(RewardAllMembersOfClanAsync(clan.ClanID, reward));
            }
            await Task.WhenAll(taskList);

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }
    }
}