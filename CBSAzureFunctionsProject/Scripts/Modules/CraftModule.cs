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
    public class CraftModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.GetRecipeDependencyStateMethod)]
        public static async Task<dynamic> GetRecipeDependencyStateTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();

            var getResult = await GetRecipeDependencyStateAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.CraftItemFromRecipeMethod)]
        public static async Task<dynamic> CraftItemFromRecipeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var recipeInventoryItemID = request.ID;

            var craftResult = await CraftItemFromRecipeAsync(profileID, recipeInventoryItemID);
            if (craftResult.Error != null)
            {
                return ErrorHandler.ThrowError(craftResult.Error).AsFunctionResult();
            }

            return craftResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.CraftItemWithoutRecipeMethod)]
        public static async Task<dynamic> CraftItemWithoutRecipeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var recipeID = request.ID;

            var craftResult = await CraftItemWithoutRecipeAsync(profileID, recipeID);
            if (craftResult.Error != null)
            {
                return ErrorHandler.ThrowError(craftResult.Error).AsFunctionResult();
            }

            return craftResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetItemNextUpgradeStateMethod)]
        public static async Task<dynamic> GetItemNextUpgradeStateTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var instanceID = request.ID;

            var getResult = await GetItemNextUpgradeStateAsync(profileID, instanceID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpgradeItemWithNextStateMethod)]
        public static async Task<dynamic> UpgradeItemWithNextStateTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var instanceID = request.ID;

            var upgradeResult = await UpgradeItemToNextStateAsync(profileID, instanceID);
            if (upgradeResult.Error != null)
            {
                return ErrorHandler.ThrowError(upgradeResult.Error).AsFunctionResult();
            }

            return upgradeResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionGetRecipeDepencencyState>> GetRecipeDependencyStateAsync(FunctionIDRequest request)
        {
            var profileID = request.ProfileID;
            var itemID = request.ID;

            var metaDataResult = await ItemsModule.GetItemsMetaDataAsync();
            if (metaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetRecipeDepencencyState>(metaDataResult.Error);
            }
            var metaData = metaDataResult.Result;
            var recipes = metaData.Recipes;
            var recipeData = recipes.GetRecipe(itemID);
            if (recipeData == null)
            {
                return ErrorHandler.RecipeItemNotFound<FunctionGetRecipeDepencencyState>();
            }

            var getDedendencyStateResult = await GetDependencyStateAsync(recipeData, profileID, string.Empty);
            if (getDedendencyStateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetRecipeDepencencyState>(getDedendencyStateResult.Error);
            }
            var itemTargetID = recipeData.ItemIdToGraft;
            var dependencyState = getDedendencyStateResult.Result;

            return new ExecuteResult<FunctionGetRecipeDepencencyState>
            {
                Result = new FunctionGetRecipeDepencencyState
                {
                    ItemIDToCraft = itemTargetID,
                    DependencyState = dependencyState
                }
            };
        }

        public static async Task<ExecuteResult<CraftStateContainer>> GetDependencyStateAsync(CBSItemDependency dependency, string profileID, string itemInstanceID)
        {
            var getInventoryResult = await InventoryModule.GetProfileInventoryAsync(profileID);
            if (getInventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<CraftStateContainer>(getInventoryResult.Error);
            }
            var getCurrenciesResult = await CurrencyModule.GetProfileCurrenciesAsync(profileID);
            if (getCurrenciesResult.Error != null)
            {
                return ErrorHandler.ThrowError<CraftStateContainer>(getCurrenciesResult.Error);
            }

            var inventory = getInventoryResult.Result;
            if (!string.IsNullOrEmpty(itemInstanceID))
            {
                var targetInstance = inventory.FirstOrDefault(x=>x.ItemInstanceId == itemInstanceID);
                if (targetInstance != null)
                {
                    inventory.Remove(targetInstance);
                }
            }
            var currencies = getCurrenciesResult.Result.Currencies;
            var needItems = dependency.ItemsDependencies;
            var needCurrencies = dependency.CurrencyDependecies;

            var itemsState = new Dictionary<string, ItemDependencyState>();
            var currenciesState = new Dictionary<string, ItemDependencyState>();

            // calculate items
            for (int i = 0; i < needItems.Count; i++)
            {
                var itemPair = needItems.ElementAt(i);
                var itemID = itemPair.Key;
                var needValue = itemPair.Value;
                var hasValue = 0;
                var anyItem = inventory.FirstOrDefault(x=>x.ItemId == itemID);
                if (anyItem != null)
                {
                    var isConsumable = anyItem.RemainingUses != null;
                    if (isConsumable)
                    {
                        hasValue = inventory.Where(x=>x.ItemId == itemID).Select(x=>x.RemainingUses == null ? 0 : (int)x.RemainingUses).Sum();
                    }
                    else
                    {
                        hasValue = inventory.Where(x=>x.ItemId == itemID).Count();
                    }
                }
                itemsState[itemID] = new ItemDependencyState
                {
                    ID = itemID,
                    NeedCount = (int)needValue,
                    PresentCount = hasValue,
                    Type = ItemDependencyType.ITEM
                };
            }
            // calculate currencies
            for (int i = 0; i < needCurrencies.Count; i++)
            {
                var currenciesPair = needCurrencies.ElementAt(i);
                var currencyCode = currenciesPair.Key;
                var needValue = currenciesPair.Value;
                var hasValue = currencies.ContainsKey(currencyCode) ? (int)currencies[currencyCode].Value : 0;
                currenciesState[currencyCode] = new ItemDependencyState
                {
                    ID = currencyCode,
                    NeedCount = (int)needValue,
                    PresentCount = hasValue,
                    Type = ItemDependencyType.CURRENCY
                };
            }

            return new ExecuteResult<CraftStateContainer>
            {
                Result = new CraftStateContainer
                {
                    ItemsState = itemsState,
                    CurrenciesState = currenciesState
                }
            };
        }

        public static async Task<ExecuteResult<FunctionCraftFromRecipeResult>> CraftItemFromRecipeAsync(string profileID, string recipeInstanceID)
        {
            var getInventoryResult = await InventoryModule.GetProfileInventoryAsync(profileID);
            if (getInventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCraftFromRecipeResult>(getInventoryResult.Error);
            }
            var inventory = getInventoryResult.Result;
            var recipeInstance = inventory.FirstOrDefault(x=>x.ItemInstanceId == recipeInstanceID);
            if (recipeInstance == null)
            {
                return ErrorHandler.ItemInstanceNotFound<FunctionCraftFromRecipeResult>();
            }
            var itemID = recipeInstance.ItemId;

            var getMetaDataResult = await ItemsModule.GetItemsMetaDataAsync();
            if (getMetaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCraftFromRecipeResult>(getMetaDataResult.Error);
            }
            var metaData = getMetaDataResult.Result;
            var recipes = metaData.Recipes;
            var recipe = recipes.GetRecipe(itemID);
            if (recipe == null)
            {
                return ErrorHandler.RecipeItemNotFound<FunctionCraftFromRecipeResult>();
            }
            var itemIDToCraft = recipe.ItemIdToGraft;

            var getDependencyResult = await GetDependencyStateAsync(recipe, profileID, recipeInstanceID);
            if (getDependencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCraftFromRecipeResult>(getDependencyResult.Error);
            }
            var dependencyContainer = getDependencyResult.Result;

            if (!dependencyContainer.ReadyToGraft())
            {
                return ErrorHandler.InsufficientFundsError<FunctionCraftFromRecipeResult>();
            }

            var payResult = await PayForDependencyAsync(recipe, profileID, recipeInstanceID);
            if (payResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCraftFromRecipeResult>(payResult.Error);
            }

            // remove recipe
            var revokeResult = await InventoryModule.RevokeInventoryItemsFromProfileAsync(profileID, new string[]{recipeInstanceID});
            if (revokeResult.Error != null)
            {
                ErrorHandler.ThrowError<FunctionCraftFromRecipeResult>(revokeResult.Error);
            }

            // grant item from recipe
            var grantRequest = new GrantItemsToProfileRequest
            {
                ProfileID = profileID,
                ItemsIDs = new string[] { itemIDToCraft }
            };
            var grantResult = await ItemsModule.GrantItemsToProfileAsync(grantRequest);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCraftFromRecipeResult>(grantResult.Error);
            }
            var grantedInstance = grantResult.Result.GrantedInstances.FirstOrDefault();

            return new ExecuteResult<FunctionCraftFromRecipeResult>
            {
                Result = new FunctionCraftFromRecipeResult
                {
                    CraftedInstance = grantedInstance,
                    SpendedCurrencies = payResult.Result.SpendedCurrencies,
                    SpendedInstanesIDs = payResult.Result.SpendedInstanesIDs,
                    ConsumedItems = payResult.Result.ConsumedItems
                }
            };
        }

        public static async Task<ExecuteResult<FunctionCraftFromRecipeResult>> CraftItemWithoutRecipeAsync(string profileID, string recipeID)
        {
            var getMetaDataResult = await ItemsModule.GetItemsMetaDataAsync();
            if (getMetaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCraftFromRecipeResult>(getMetaDataResult.Error);
            }
            var metaData = getMetaDataResult.Result;
            var recipes = metaData.Recipes;
            var recipe = recipes.GetRecipe(recipeID);
            if (recipe == null)
            {
                return ErrorHandler.RecipeItemNotFound<FunctionCraftFromRecipeResult>();
            }
            var itemIDToCraft = recipe.ItemIdToGraft;

            var getDependencyResult = await GetDependencyStateAsync(recipe, profileID, string.Empty);
            if (getDependencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCraftFromRecipeResult>(getDependencyResult.Error);
            }
            var dependencyContainer = getDependencyResult.Result;

            if (!dependencyContainer.ReadyToGraft())
            {
                return ErrorHandler.InsufficientFundsError<FunctionCraftFromRecipeResult>();
            }

            var payResult = await PayForDependencyAsync(recipe, profileID, string.Empty);
            if (payResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCraftFromRecipeResult>(payResult.Error);
            }

            // grant item from recipe
            var grantRequest = new GrantItemsToProfileRequest
            {
                ProfileID = profileID,
                ItemsIDs = new string[] { itemIDToCraft }
            };
            var grantResult = await ItemsModule.GrantItemsToProfileAsync(grantRequest);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCraftFromRecipeResult>(grantResult.Error);
            }
            var grantedInstance = grantResult.Result.GrantedInstances.FirstOrDefault();

            return new ExecuteResult<FunctionCraftFromRecipeResult>
            {
                Result = new FunctionCraftFromRecipeResult
                {
                    CraftedInstance = grantedInstance,
                    SpendedCurrencies = payResult.Result.SpendedCurrencies,
                    SpendedInstanesIDs = payResult.Result.SpendedInstanesIDs,
                    ConsumedItems = payResult.Result.ConsumedItems
                }
            };
        }

        public static async Task<ExecuteResult<FunctionPayForDependencyResult>> PayForDependencyAsync(CBSItemDependency dependency, string profileID, string itemInstanceID)
        {
            var getInventoryResult = await InventoryModule.GetProfileInventoryAsync(profileID);
            if (getInventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPayForDependencyResult>(getInventoryResult.Error);
            }
            var getCurrenciesResult = await CurrencyModule.GetProfileCurrenciesAsync(profileID);
            if (getCurrenciesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPayForDependencyResult>(getCurrenciesResult.Error);
            }

            var inventory = getInventoryResult.Result;
            if (!string.IsNullOrEmpty(itemInstanceID))
            {
                var targetInstance = inventory.FirstOrDefault(x=>x.ItemInstanceId == itemInstanceID);
                if (targetInstance != null)
                {
                    inventory.Remove(targetInstance);
                }
            }
            var currencies = getCurrenciesResult.Result.Currencies;
            var needItems = dependency.ItemsDependencies;
            var needCurrencies = dependency.CurrencyDependecies;

            var itemsToRevoke = new List<string>();
            var itemsToConsume = new Dictionary<string, uint>();
            var currenciesToDescrease = new Dictionary<string, uint>();

            // calculate items
            for (int i = 0; i < needItems.Count; i++)
            {
                var itemPair = needItems.ElementAt(i);
                var itemID = itemPair.Key;
                var needValue = itemPair.Value;
                var defaultItem = inventory.FirstOrDefault(x=>x.ItemId == itemID);
                if (defaultItem == null)
                    return ErrorHandler.InsufficientFundsError<FunctionPayForDependencyResult>();
                var isConsumable = defaultItem.RemainingUses != null;
                if (isConsumable)
                {
                    var inventoryInstances = inventory.Where(x=>x.ItemId == itemID && x.RemainingUses != null);
                    var hasValue = inventoryInstances == null ? 0 : inventoryInstances.Select(x=>x.RemainingUses == null ? 0 : (int)x.RemainingUses).Sum();
                    var needCounter = needValue;
                    if (needValue > hasValue)
                    {
                        return ErrorHandler.InsufficientFundsError<FunctionPayForDependencyResult>();
                    }
                    foreach (var inventoryInstance in inventoryInstances)
                    {
                        var usageCount = (uint)inventoryInstance.RemainingUses;
                        if (usageCount > needValue)
                        {
                            itemsToConsume[inventoryInstance.ItemInstanceId] = needValue;
                            break;
                        }
                        else
                        {
                            itemsToRevoke.Add(inventoryInstance.ItemInstanceId);
                        }
                        needValue -= usageCount;
                    }
                }
                else
                {
                    var instanceIDs = inventory.Where(x=>x.ItemId == itemID).Select(x=>x.ItemInstanceId).ToList();
                    if (instanceIDs != null && needValue > instanceIDs.Count)
                    {
                        return ErrorHandler.InsufficientFundsError<FunctionPayForDependencyResult>();
                    }
                    var instancesToRevoke = instanceIDs.Take((int)needValue);
                    foreach (var instanceID in instancesToRevoke)
                    {
                        itemsToRevoke.Add(instanceID);
                    }
                }
            }

            // calculate cuurencies
            foreach (var currencyPair in needCurrencies)
            {
                var needKey = currencyPair.Key;
                var needValue = currencyPair.Value;

                if (!currencies.ContainsKey(needKey) || currencies[needKey].Value < needValue)
                {
                    return ErrorHandler.InsufficientFundsError<FunctionPayForDependencyResult>();
                }

                currenciesToDescrease[needKey] = needValue;
            }

            // substact all currency
            var substactTasks = new List<Task<ExecuteResult<FunctionChangeCurrencyResult>>>();
            foreach (var substractPair in currenciesToDescrease)
            {
                var substactTask = CurrencyModule.SubtractVirtualCurrencyFromProfileAsync(profileID, substractPair.Key, (int)substractPair.Value);
                substactTasks.Add(substactTask);
            }
            var substactResult = await Task.WhenAll(substactTasks);
            if (substactResult.Any(x=>x.Error != null))
            {
                var error = substactResult.FirstOrDefault(x=>x.Error != null);
                return ErrorHandler.ThrowError<FunctionPayForDependencyResult>(error.Error);
            }

            // revoke items
            var revokedInstances = new List<string>();
            var revokeTasks = new List<Task<ExecuteResult<FunctionRevokeInventoryItemsResult>>>();
            var revokeRequestSplit = itemsToRevoke.ToArray().Split<string>(PlayfabHelper.MaxInventoryRevokeCountByRequest);
            foreach (var slitArray in revokeRequestSplit)
            {
                var revokeTask = InventoryModule.RevokeInventoryItemsFromProfileAsync(profileID, slitArray.ToArray());
                revokeTasks.Add(revokeTask);
            }
            var revokeResult = await Task.WhenAll(revokeTasks);
            if (revokeResult.Any(x=>x.Error != null))
            {
                var error = revokeResult.FirstOrDefault(x=>x.Error != null);
                return ErrorHandler.ThrowError<FunctionPayForDependencyResult>(error.Error);
            }
            var revomedInstanceIDs = revokeResult.Select(x=>x.Result.RevomedInstanceIDs);
            foreach (var removeSplit in revomedInstanceIDs)
            {
                revokedInstances = revokedInstances.Concat(removeSplit.ToList()).ToList();
            }

            // consume items
            var consumeItemsTasks = new List<Task<ExecuteResult<FunctionModifyUsesResult>>>();
            foreach (var consumePair in itemsToConsume)
            {
                var consumeItemsTask = InventoryModule.ConsumeItemAsync(profileID, consumePair.Key, (int)consumePair.Value);
                consumeItemsTasks.Add(consumeItemsTask);
            }
            var consumeResult = await Task.WhenAll(consumeItemsTasks);

            return new ExecuteResult<FunctionPayForDependencyResult>
            {
                Result = new FunctionPayForDependencyResult
                {
                    SpendedCurrencies = currenciesToDescrease,
                    SpendedInstanesIDs = revokedInstances,
                    ConsumedItems = itemsToConsume
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetNextUpgradeStateResult>> GetItemNextUpgradeStateAsync(string profileID, string inventoryItemID)
        {
            var getInventoryResult = await InventoryModule.GetProfileInventoryAsync(profileID);
            if (getInventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetNextUpgradeStateResult>(getInventoryResult.Error);
            }
            var inventory = getInventoryResult.Result;

            var itemInstance = inventory.FirstOrDefault(x=>x.ItemInstanceId == inventoryItemID);
            if (itemInstance == null)
            {
                return ErrorHandler.ItemInstanceNotFound<FunctionGetNextUpgradeStateResult>();
            }

            var itemID = itemInstance.ItemId;

            var metaDataResult = await ItemsModule.GetItemsMetaDataAsync();
            if (metaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetNextUpgradeStateResult>(metaDataResult.Error);
            }
            var metaData = metaDataResult.Result;
            var upgrades = metaData.Upgrades;
            var upgradeDataList = upgrades.GetUpdgrades(itemID);
            if (upgradeDataList == null || upgradeDataList.Count == 0)
            {
                return ErrorHandler.ItemIsNotUpgradable<FunctionGetNextUpgradeStateResult>();
            }

            var instanceCustomData = itemInstance.CustomData;
            var hasIndexData = instanceCustomData != null && instanceCustomData.Count > 0 && instanceCustomData.ContainsKey(ItemDataKeys.UpgradeIndexKey);
            var upgradeIndex = hasIndexData ? int.Parse(instanceCustomData[ItemDataKeys.UpgradeIndexKey]) : 0;
            var isMax = upgradeIndex >= upgradeDataList.Count - 1;
            var dependencyState = new CraftStateContainer();
            if (!isMax)
            {
                var nextUpgradeState = upgradeDataList.ElementAt(upgradeIndex + 1);
                var getDedendencyStateResult = await GetDependencyStateAsync(nextUpgradeState, profileID, inventoryItemID);
                if (getDedendencyStateResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGetNextUpgradeStateResult>(getDedendencyStateResult.Error);
                }
                dependencyState = getDedendencyStateResult.Result;
            }

            return new ExecuteResult<FunctionGetNextUpgradeStateResult>
            {
                Result = new FunctionGetNextUpgradeStateResult
                {
                    ProfileID = profileID,
                    ItemID = itemID,
                    InventoryItemID = inventoryItemID,
                    IsMax = isMax,
                    NextUpgradeIndex = isMax ? upgradeIndex : upgradeIndex + 1,
                    CurrentUpgradeIndex = upgradeIndex,
                    NextUpgradeState = isMax ? null : upgradeDataList.ElementAt(upgradeIndex + 1),
                    DependencyState = isMax ? null : dependencyState
                }
            };
        }

        public static async Task<ExecuteResult<FunctionUpgradeItemResult>> UpgradeItemToNextStateAsync(string profileID, string inventoryItemID)
        {
            var getUpgradeDependencyResult = await GetItemNextUpgradeStateAsync(profileID, inventoryItemID);
            if (getUpgradeDependencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpgradeItemResult>(getUpgradeDependencyResult.Error);
            }
            var upgradeDependency = getUpgradeDependencyResult.Result;
            var craftDependency = upgradeDependency.DependencyState;
            var upgradeState = upgradeDependency.NextUpgradeState;
            var upgradeIndex = upgradeDependency.NextUpgradeIndex;
            var isMax = upgradeDependency.IsMax;
            if (isMax)
            {
                return ErrorHandler.MaxUpgradeReached<FunctionUpgradeItemResult>();
            }
            var payForDependencyResult = await PayForDependencyAsync(upgradeState, profileID, inventoryItemID);
            if (payForDependencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpgradeItemResult>(payForDependencyResult.Error);
            }
            var payData = payForDependencyResult.Result;

            var updateDataResult = await InventoryModule.UpdateInventoryItemCustomDataByKeyAsync(profileID, inventoryItemID, ItemDataKeys.UpgradeIndexKey, upgradeIndex.ToString());
            if (updateDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpgradeItemResult>(updateDataResult.Error);
            }

            var getInstanceResult = await InventoryModule.GetItemInstanceByInventoryIDAsync(profileID, inventoryItemID);
            if (getInstanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpgradeItemResult>(getInstanceResult.Error);
            }
            var fabInstance = getInstanceResult.Result;

            return new ExecuteResult<FunctionUpgradeItemResult>
            {
                Result = new FunctionUpgradeItemResult
                {
                    ProfileID = profileID,
                    UpgradedItem = fabInstance.ToClientInstance(),
                    UpgradedLevelIndex = upgradeIndex,
                    SpendedCurrencies = payData.SpendedCurrencies,
                    SpendedInstanesIDs = payData.SpendedInstanesIDs,
                    ConsumedItems = payData.ConsumedItems
                }
            };
        }
    }
}