using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSCraftingModule : CBSModule, ICrafting
    {
        /// <summary>
        /// Notify when item was crafted.
        /// </summary>
        public event Action<CBSInventoryItem> OnItemCrafted;

        /// <summary>
        /// Notify when item was upgraded.
        /// </summary>
        public event Action<CBSInventoryItem> OnItemUpgraded;

        private IFabCrafting FabCrafting { get; set; }
        private IProfile Profile { get; set; }
        private ICBSInventory Inventory { get; set; }
        private ICurrency Currency { get; set; }
        private ICBSItems Items { get; set; }

        protected override void Init()
        {
            FabCrafting = FabExecuter.Get<FabCrafting>();
            Profile = Get<CBSProfileModule>();
            Inventory = Get<CBSInventoryModule>();
            Currency = Get<CBSCurrencyModule>();
            Items = Get<CBSItemsModule>();
        }

        /// <summary>
        /// Get the status of all ingredients for a craft item.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void GetRecipeDependencyState(string itemID, Action<CBSGetRecipeDependencyStateResult> result)
        {
            var profileID = Profile.ProfileID;
            FabCrafting.GetRecipeDependencyState(profileID, itemID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetRecipeDependencyStateResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetRecipeDepencencyState>();
                    var state = functionResult.DependencyState;
                    var itemIDToCraft = functionResult.ItemIDToCraft;

                    result?.Invoke(new CBSGetRecipeDependencyStateResult
                    {
                        IsSuccess = true,
                        ItemIDToCraft = itemIDToCraft,
                        DependencyState = state
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetRecipeDependencyStateResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get the status of all ingredients for a craft item from cache. Requires "Preload Inventory", "Preload Currency" options enabled.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void GetRecipeDependencyStateFromCache(string itemID, Action<CBSGetRecipeDependencyStateResult> result)
        {
            var profileID = Profile.ProfileID;

            var recipe = Items.GetFromCache(itemID) as CBSItem;
            if (recipe == null || !recipe.IsRecipe || recipe.RecipeData == null)
            {
                result?.Invoke(new CBSGetRecipeDependencyStateResult
                {
                    IsSuccess = false,
                    Error = CBSError.RecipeNotFoundError()
                });
            }

            var craftContainer = CalculateDependencyFromCache(recipe.RecipeData, string.Empty);
            var itemIDToGraft = recipe.RecipeData.ItemIdToGraft;

            result?.Invoke(new CBSGetRecipeDependencyStateResult
            {
                IsSuccess = true,
                ItemIDToCraft = itemIDToGraft,
                DependencyState = craftContainer
            });
        }

        /// <summary>
        /// Craft item by inventory id (Instance ID) of the recipe. Requires instance of recipe in inventory.
        /// </summary>
        /// <param name="recipeInventoryID"></param>
        /// <param name="result"></param>
        public void CraftItemFromRecipe(string recipeInventoryID, Action<CBSCraftResult> result)
        {
            var profileID = Profile.ProfileID;
            FabCrafting.CraftItemFromRecipe(profileID, recipeInventoryID, onCraft =>
            {
                var cbsError = onCraft.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSCraftResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onCraft.GetResult<FunctionCraftFromRecipeResult>();
                    var fabInstance = functionResult.CraftedInstance;
                    var spendedCurrencies = functionResult.SpendedCurrencies;
                    var spendedInstances = functionResult.SpendedInstanesIDs;
                    var consumedItems = functionResult.ConsumedItems;

                    Get<CBSCurrencyModule>().ChangeRequest(spendedCurrencies.Select(x => x.Key).ToArray());
                    Get<CBSInventoryModule>().RevokeRequest(spendedInstances);
                    Get<CBSInventoryModule>().ConsumeSpendRequest(consumedItems);

                    result?.Invoke(new CBSCraftResult
                    {
                        IsSuccess = true,
                        CraftedItemInstance = fabInstance.ToCBSInventoryItem(),
                        SpendedCurrencies = spendedCurrencies,
                        SpendedInstancesIDs = spendedInstances,
                        ConsumedItems = consumedItems
                    });

                    OnItemCrafted?.Invoke(fabInstance.ToCBSInventoryItem());
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSCraftResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Craft item by item id (Catalog Item ID) of the recipe. Does not require an instance of recipe in inventory.
        /// </summary>
        /// <param name="recipeID"></param>
        /// <param name="result"></param>
        public void CraftItemFromRecipeTemplate(string recipeID, Action<CBSCraftResult> result)
        {
            var profileID = Profile.ProfileID;
            FabCrafting.CraftItemWithoutRecipe(profileID, recipeID, onCraft =>
            {
                var cbsError = onCraft.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSCraftResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onCraft.GetResult<FunctionCraftFromRecipeResult>();
                    var fabInstance = functionResult.CraftedInstance;
                    var spendedCurrencies = functionResult.SpendedCurrencies;
                    var spendedInstances = functionResult.SpendedInstanesIDs;
                    var consumedItems = functionResult.ConsumedItems;

                    Get<CBSCurrencyModule>().ChangeRequest(spendedCurrencies.Select(x => x.Key).ToArray());
                    Get<CBSInventoryModule>().RevokeRequest(spendedInstances);
                    Get<CBSInventoryModule>().ConsumeSpendRequest(consumedItems);

                    result?.Invoke(new CBSCraftResult
                    {
                        IsSuccess = true,
                        CraftedItemInstance = fabInstance.ToCBSInventoryItem(),
                        SpendedCurrencies = spendedCurrencies,
                        SpendedInstancesIDs = spendedInstances,
                        ConsumedItems = consumedItems
                    });

                    OnItemCrafted?.Invoke(fabInstance.ToCBSInventoryItem());
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSCraftResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get the status of next item upgrade, include ingredients dependency
        /// </summary>
        /// <param name="itemInstanceID"></param>
        /// <param name="result"></param>
        public void GetItemNextUpgradeState(string itemInstanceID, Action<CBSGetNextUpgradeStateResult> result)
        {
            var profileID = Profile.ProfileID;
            FabCrafting.GetItemNextUpgradeState(profileID, itemInstanceID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetNextUpgradeStateResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetNextUpgradeStateResult>();
                    var state = functionResult.DependencyState;
                    var ownerID = functionResult.ProfileID;
                    var itemID = functionResult.ItemID;
                    var inventoryID = functionResult.InventoryItemID;
                    var isMax = functionResult.IsMax;
                    var nextIndex = functionResult.NextUpgradeIndex;
                    var nextState = functionResult.NextUpgradeState;
                    var currentIndex = functionResult.CurrentUpgradeIndex;

                    result?.Invoke(new CBSGetNextUpgradeStateResult
                    {
                        IsSuccess = true,
                        ProfileID = ownerID,
                        DependencyState = state,
                        ItemID = itemID,
                        ItemInstanceID = inventoryID,
                        IsMax = isMax,
                        NextUpgradeIndex = nextIndex,
                        CurrentUpgradeIndex = currentIndex,
                        NextUpgradeState = nextState
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetNextUpgradeStateResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get the status of next item upgrade from cache, include ingredients dependency. Requires "Preload Inventory", "Preload Currency" options enabled.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void GetItemNextUpgradeStateFromCache(string inventoryItemID, Action<CBSGetNextUpgradeStateResult> result)
        {
            var profileID = Profile.ProfileID;

            var upgradeItem = Inventory.GetInventoryItemFromCache(inventoryItemID);
            if (upgradeItem == null || !upgradeItem.IsUpgradable)
            {
                result?.Invoke(new CBSGetNextUpgradeStateResult
                {
                    IsSuccess = false,
                    Error = CBSError.UpgradeNotFoundError()
                });
            }
            var isMax = upgradeItem.IsMaxUpgrade();
            var nextState = upgradeItem.GetNextUpgradeData();

            var craftContainer = CalculateDependencyFromCache(nextState, inventoryItemID);
            var itemID = upgradeItem.ItemID;
            var nextIndex = isMax ? upgradeItem.UpgradeIndex : upgradeItem.UpgradeIndex + 1;
            var currentIndex = upgradeItem.UpgradeIndex;

            result?.Invoke(new CBSGetNextUpgradeStateResult
            {
                IsSuccess = true,
                ProfileID = profileID,
                DependencyState = craftContainer,
                ItemID = itemID,
                ItemInstanceID = inventoryItemID,
                IsMax = isMax,
                NextUpgradeIndex = nextIndex,
                CurrentUpgradeIndex = currentIndex,
                NextUpgradeState = nextState
            });
        }

        /// <summary>
        /// Upgrade item to next level.
        /// </summary>
        /// <param name="inventoryItemID"></param>
        /// <param name="result"></param>
        public void UpgradeItemToNextLevel(string inventoryItemID, Action<CBSUpgradeItemResult> result)
        {
            var profileID = Profile.ProfileID;
            FabCrafting.UpgradeItemWithNextState(profileID, inventoryItemID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSUpgradeItemResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionUpgradeItemResult>();
                    var newIndex = functionResult.UpgradedLevelIndex;
                    var fabInstance = functionResult.UpgradedItem;
                    var ownerID = functionResult.ProfileID;
                    var spendedCurrencies = functionResult.SpendedCurrencies;
                    var spendedInstances = functionResult.SpendedInstanesIDs;
                    var consumedItems = functionResult.ConsumedItems;

                    Get<CBSCurrencyModule>().ChangeRequest(spendedCurrencies.Select(x => x.Key).ToArray());
                    Get<CBSInventoryModule>().RevokeRequest(spendedInstances);
                    Get<CBSInventoryModule>().ConsumeSpendRequest(consumedItems);
                    Get<CBSInventoryModule>().UpdateRequest(fabInstance.ToCBSInventoryItem());

                    result?.Invoke(new CBSUpgradeItemResult
                    {
                        IsSuccess = true,
                        ProfileID = ownerID,
                        UpgradedLevelIndex = newIndex,
                        UpgradedItem = fabInstance.ToCBSInventoryItem(),
                        SpendedCurrencies = spendedCurrencies,
                        SpendedInstanesIDs = spendedInstances,
                        ConsumedItems = consumedItems
                    });

                    OnItemUpgraded?.Invoke(fabInstance.ToCBSInventoryItem());
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSUpgradeItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        // internal
        private CraftStateContainer CalculateDependencyFromCache(CBSItemDependency dependency, string itemInstanceID)
        {
            var inventory = Inventory.GetInventoryFromCache().NonEquippedItems;
            var currencies = Currency.CacheCurrencies;
            var needItems = dependency == null ? new Dictionary<string, uint>() : dependency.ItemsDependencies;
            var needCurrencies = dependency == null ? new Dictionary<string, uint>() : dependency.CurrencyDependecies;
            
            if (!string.IsNullOrEmpty(itemInstanceID))
            {
                var targetInstance = inventory.FirstOrDefault(x=>x.InstanceID == itemInstanceID);
                if (targetInstance != null)
                {
                    inventory.Remove(targetInstance);
                }
            }

            var itemsState = new Dictionary<string, ItemDependencyState>();
            var currenciesState = new Dictionary<string, ItemDependencyState>();

            // calculate items
            for (int i = 0; i < needItems.Count; i++)
            {
                var itemPair = needItems.ElementAt(i);
                var dependencyItemID = itemPair.Key;
                var needValue = itemPair.Value;
                var hasValue = 0;
                var anyItem = inventory.FirstOrDefault(x => x.ItemID == dependencyItemID);
                if (anyItem != null)
                {
                    var isConsumable = anyItem.IsConsumable;
                    if (isConsumable)
                    {
                        hasValue = inventory.Where(x => x.ItemID == dependencyItemID).Select(x => (int)x.Count).Sum();
                    }
                    else
                    {
                        hasValue = inventory.Where(x => x.ItemID == dependencyItemID).Count();
                    }
                }
                itemsState[dependencyItemID] = new ItemDependencyState
                {
                    ID = dependencyItemID,
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

            return new CraftStateContainer
            {
                ItemsState = itemsState,
                CurrenciesState = currenciesState
            };
        }
    }
}
