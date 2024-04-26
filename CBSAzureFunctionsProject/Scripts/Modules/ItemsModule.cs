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
using CBS.SharedData.Lootbox;

namespace CBS
{
    public class ItemsModule : BaseAzureModule
    {
        private static readonly string ItemsCatalogID = CatalogKeys.ItemsCatalogID;

        [FunctionName(AzureFunctions.FetchItemsMethod)]
        public static async Task<dynamic> GetProfileCurrencyTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            var getResult = await FetchItemsResultAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetItemsCategoriesMethod)]
        public static async Task<dynamic> GetItemsCategoriesTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            var getResult = await GetItemsCategoriesRawDataAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetFabItemsMethod)]
        public static async Task<dynamic> GetFabItemsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetItemsRequest>();

            var getResult = await GetItemsAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetFabItemByIDMethod)]
        public static async Task<dynamic> GetFabItemByIDTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var itemID = request.ID;

            var getResult = await GetItemByIDAsync(itemID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PostPurchaseProccessMethod)]
        public static async Task<dynamic> PostPurchaseProccessTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionPostPurchaseRequest>();
            var profileID = request.ProfileID;
            var itemID = request.ItemID;

            var postResult = await PostPurchaseProccessAsync(profileID, itemID);
            if (postResult.Error != null)
            {
                return ErrorHandler.ThrowError(postResult.Error).AsFunctionResult();
            }

            return postResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GrantItemsToProfileMethod)]
        public static async Task<dynamic> GrantItemsToProfileTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<GrantItemsToProfileRequest>();

            var grantResult = await GrantItemsToProfileAsync(request);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError(grantResult.Error).AsFunctionResult();
            }

            return grantResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<GetCatalogItemsResult>> GetCatalogItemsAsync()
        {
            var request = new GetCatalogItemsRequest
            {
                CatalogVersion = ItemsCatalogID
            };
            var itemsResult = await FabServerAPI.GetCatalogItemsAsync(request);
            if (itemsResult.Error != null)
            {
                return ErrorHandler.ThrowError<GetCatalogItemsResult>(itemsResult.Error);
            }
            return new ExecuteResult<GetCatalogItemsResult>{
                Result = itemsResult.Result
            };
        }

        public static async Task<ExecuteResult<FunctionGetCategoriesResult>> GetItemsCategoriesRawDataAsync()
        {
            var titleResult = await GetRawInternalTitlesDataAsync(new List<string>() {
                TitleKeys.ItemsCategoriesKey,
                TitleKeys.LootboxesCategoriesKey,
                TitleKeys.PackCategoriesKey
            });
            if (titleResult.Error != null)
            {
                return ErrorHandler.ThrowError<Dictionary<string, string>>(titleResult.Error);
            }
            var data = titleResult.Result;
            return new ExecuteResult<FunctionGetCategoriesResult>{
                Result = new FunctionGetCategoriesResult
                {
                    CategoriesData = data
                }
            };
        }

        public static async Task<ExecuteResult<FunctionFetchItemsResult>> FetchItemsResultAsync()
        {
            var itemsResult = await GetCatalogItemsAsync();
            if (itemsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFetchItemsResult>(itemsResult.Error);
            }
            var items = itemsResult.Result;

            var categoriesResult = await GetItemsCategoriesRawDataAsync();
            if (categoriesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFetchItemsResult>(categoriesResult.Error);
            }
            var categories = categoriesResult.Result;

            var metaDataResult = await GetItemsMetaDataAsync();
            if (metaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFetchItemsResult>(metaDataResult.Error);
            }
            var metaData = metaDataResult.Result;
            var recipe = metaData.Recipes;
            var upgrade = metaData.Upgrades;
            var lootboxTable = metaData.LootboxTable;

            return new ExecuteResult<FunctionFetchItemsResult>
            {
                Result = new FunctionFetchItemsResult
                {
                    ItemsResult = items.ToClientInstance(),
                    Categories = categories.CategoriesData,
                    Recipes = recipe,
                    Upgrades = upgrade,
                    LootboxTable = lootboxTable
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetItemsMetaData>> GetItemsMetaDataAsync()
        {
            var titleKeys = new List<string>
            {
                TitleKeys.ItemsRecipeKey,
                TitleKeys.ItemsUpgradeKey,
                TitleKeys.LootboxTableKey,
            };

            var getDataResult = await GetRawInternalTitlesDataAsync(titleKeys);
            if (getDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetItemsMetaData>(getDataResult.Error);
            }
            var data = getDataResult.Result;
            var recipeRaw = data.ContainsKey(TitleKeys.ItemsRecipeKey) ? data[TitleKeys.ItemsRecipeKey] : JsonPlugin.EMPTY_JSON;
            recipeRaw = string.IsNullOrEmpty(recipeRaw) ? JsonPlugin.EMPTY_JSON : recipeRaw;
            var upgradeRaw = data.ContainsKey(TitleKeys.ItemsUpgradeKey) ? data[TitleKeys.ItemsUpgradeKey] : JsonPlugin.EMPTY_JSON;
            upgradeRaw = string.IsNullOrEmpty(upgradeRaw) ? JsonPlugin.EMPTY_JSON : upgradeRaw;
            var lootboxRaw = data.ContainsKey(TitleKeys.LootboxTableKey) ? data[TitleKeys.LootboxTableKey] : JsonPlugin.EMPTY_JSON;
            lootboxRaw = string.IsNullOrEmpty(lootboxRaw) ? JsonPlugin.EMPTY_JSON : lootboxRaw;

            var recipeContainer = new CBSRecipeContainer();
            var upgradeContainer = new CBSItemUpgradesContainer();
            var lootboxTable = new CBSLootboxTable();

            try
            {
                recipeContainer = JsonPlugin.FromJsonDecompress<CBSRecipeContainer>(recipeRaw);
            }
            catch
            {
                recipeContainer = JsonPlugin.FromJson<CBSRecipeContainer>(recipeRaw);
            }
            try
            {
                upgradeContainer = JsonPlugin.FromJsonDecompress<CBSItemUpgradesContainer>(upgradeRaw);
            }
            catch
            {
                upgradeContainer = JsonPlugin.FromJson<CBSItemUpgradesContainer>(upgradeRaw);
            }
            try
            {
                lootboxTable = JsonPlugin.FromJsonDecompress<CBSLootboxTable>(lootboxRaw);
            }
            catch
            {
                lootboxTable = JsonPlugin.FromJson<CBSLootboxTable>(lootboxRaw);
            }

            return new ExecuteResult<FunctionGetItemsMetaData>
            {
                Result = new FunctionGetItemsMetaData
                {
                    Recipes = recipeContainer,
                    Upgrades = upgradeContainer,
                    LootboxTable = lootboxTable
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetItemsResult>> GetItemsAsync(FunctionGetItemsRequest request)
        {
            var type = request.ItemType;
            var category = request.SpecificCategory;

            var getCatalogResult = await GetCatalogItemsAsync();
            if (getCatalogResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetItemsResult>(getCatalogResult.Error);
            }
            var catalogResult = getCatalogResult.Result;
            var catalog = catalogResult.Catalog;
            if (type == ItemType.ITEMS)
                catalog = catalog.Where(x=>x.IsItem()).ToList();
            else if (type == ItemType.PACKS)
                catalog = catalog.Where(x=>x.IsPack()).ToList();
            else if (type == ItemType.LOOT_BOXES)
                catalog = catalog.Where(x=>x.IsLootbox()).ToList();

            if (!string.IsNullOrEmpty(category))
            {
                catalog = catalog.Where(x=>x.GetCategory() == category).ToList();
            }
            catalogResult.Catalog = catalog;

            return new ExecuteResult<FunctionGetItemsResult>
            {
                Result = new FunctionGetItemsResult
                {
                    ItemsResult = catalogResult.ToClientInstance()
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetItemByIDResult>> GetItemByIDAsync(string itemID)
        {
            var getCatalogResult = await GetCatalogItemsAsync();
            if (getCatalogResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetItemByIDResult>(getCatalogResult.Error);
            }
            var catalogResult = getCatalogResult.Result;
            var catalog = catalogResult.Catalog;
            var fabItem = catalog.FirstOrDefault(x=>x.ItemId == itemID);
            if (fabItem == null)
            {
                return ErrorHandler.CatalogItemNotFound<FunctionGetItemByIDResult>();
            }
            return new ExecuteResult<FunctionGetItemByIDResult>
            {
                Result = new FunctionGetItemByIDResult
                {
                    FabItem = fabItem.ToClientInstance()
                }
            };
        }

        public static async Task<ExecuteResult<FunctionPostPurchaseResult>> PostPurchaseProccessAsync(string profileID, string itemID)
        {
            var itemResult = await GetItemByIDAsync(itemID);
            if (itemResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPostPurchaseResult>(itemResult.Error);
            }
            var fabItem = itemResult.Result.FabItem;
            var bundleCurrencies = new Dictionary<string, uint>();
            if (fabItem.Bundle != null)
            {
                var bundle = fabItem.Bundle;
                var currency = bundle.BundledVirtualCurrencies;
                bundleCurrencies = currency;

                var getInventoryResult = await InventoryModule.GetProfileInventoryAsync(profileID);
                if (getInventoryResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionPostPurchaseResult>(getInventoryResult.Error);
                }

                var inventoryItems = getInventoryResult.Result;
                var bundleInstance = inventoryItems.FirstOrDefault(x=>x.ItemId == itemID);
                var bundleInstanceID = bundleInstance.ItemInstanceId;

                var removeResult = await RemoveProfileInventoryItem(profileID, bundleInstanceID);
                if (removeResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionPostPurchaseResult>(removeResult.Error);
                }
            }
            return new ExecuteResult<FunctionPostPurchaseResult>
            {
                Result = new FunctionPostPurchaseResult
                {
                    PurchasedCurrencies = bundleCurrencies
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGrantItemsResult>> GrantItemsToProfileAsync(GrantItemsToProfileRequest request)
        {
            var profileID = request.ProfileID;
            var itemsIDs = request.ItemsIDs;
            var containPack = request.ContainPack;

            var grantedCurrencies = new Dictionary<string, uint>();

            var grantRequest = new GrantItemsToUserRequest
            {
                CatalogVersion = ItemsCatalogID,
                ItemIds = itemsIDs.ToList(),
                PlayFabId = profileID
            };
            var grantResult = await FabServerAPI.GrantItemsToUserAsync(grantRequest);
            if (grantResult.Error != null)
            {
                ErrorHandler.ThrowError<FunctionGrantItemsResult>(grantResult.Error);
            }
            var fabInstances = grantResult.Result.ItemGrantResults;

            if (containPack)
            {
                var inventoryBundlesItems = fabInstances;
                var catalogItemsIDs = inventoryBundlesItems.Select(x=>x.ItemId).ToList();
                var inventoryIds = inventoryBundlesItems.Select(x=>x.ItemInstanceId);

                var itemsResult = await FabServerAPI.GetCatalogItemsAsync(new GetCatalogItemsRequest
                {
                    CatalogVersion = ItemsCatalogID
                });
                if (itemsResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGrantItemsResult>(itemsResult.Error);
                }
                var fabItems = itemsResult.Result.Catalog;
                var fabBundledItems = fabItems.Where(x=>catalogItemsIDs.Contains(x.ItemId) && x.Bundle != null && x.Bundle.BundledVirtualCurrencies != null);
                var fabBundledItemsIDs = fabBundledItems.Select(x=>x.ItemId);
                var bundlesInstances = inventoryBundlesItems.Where(x=>fabBundledItemsIDs.Contains(x.ItemId)).Select(x=>x.ItemInstanceId).ToArray();
                fabInstances = fabInstances.Where(x=>!bundlesInstances.Contains(x.ItemInstanceId)).ToList();

                foreach (var bundledItem in fabBundledItems)
                {
                    grantedCurrencies = grantedCurrencies.Concat(bundledItem.Bundle.BundledVirtualCurrencies).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => (uint)x.Sum(y=>y.Value));
                }
                var revokeResult = await InventoryModule.RevokeInventoryItemsFromProfileAsync(profileID, bundlesInstances);
                if (revokeResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGrantItemsResult>(revokeResult.Error);
                }
            }

            return new ExecuteResult<FunctionGrantItemsResult>
            {
                Result = new FunctionGrantItemsResult
                {
                    TargetID = profileID,
                    GrantedInstances = fabInstances.ToClientInstances(),
                    GrantedCurrencies = grantedCurrencies
                }
            };
        }
    }
}