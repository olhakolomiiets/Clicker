using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Linq;
using CBS.SharedData.Lootbox;
using UnityEngine;

namespace CBS
{
    public class CBSItemsModule : CBSModule, ICBSItems
    {
        /// <summary>
        /// Notify when cbs item was purchased with currencies.
        /// </summary>
        public event Action<CBSPurchaseItemWithCurrencyResult> OnItemPurchased;
        /// <summary>
        /// Notify when cbs item was purchased with real money.
        /// </summary>
        public event Action<CBSPurchaseItemWithRealMoneyResult> OnItemPurchasedWithRealMoney;
        /// <summary>
        /// Notify when items was granted to user.
        /// </summary>
        public event Action<CBSGrantItemsResult> OnItemGranted;

        /// <summary>
        /// Cached list of all cbs items available for playfab
        /// </summary>
        public List<CBSItem> AllItems { get; private set; } = new List<CBSItem>();
        /// <summary>
        /// Cached list of all cbs packs available for playfab
        /// </summary>
        public List<CBSItemPack> AllPacks { get; private set; } = new List<CBSItemPack>();
        /// <summary>
        /// Cached list of all cbs lootboxes available for playfab
        /// </summary>
        public List<CBSLootbox> AllLootboxes { get; private set; } = new List<CBSLootbox>();
        /// <summary>
        /// Cached recipes data from playfab.
        /// </summary>
        public CBSRecipeContainer Recipes { get; private set; } = new CBSRecipeContainer();
        /// <summary>
        /// Cached lootbox data from playfab.
        /// </summary>
        public CBSLootboxTable LootboxTable { get; private set; } = new CBSLootboxTable();
        /// <summary>
        /// Cached upgrades data from playfab.
        /// </summary>
        public CBSItemUpgradesContainer Upgrades { get; private set; } = new CBSItemUpgradesContainer();
        /// <summary>
        /// Dictionary of all cbs items (include packs and lutboxes).
        /// </summary>
        public Dictionary<string, CBSBaseItem> ItemsDictionary { get; private set; } = new Dictionary<string, CBSBaseItem>();
        /// <summary>
        /// Cached list of cbs items categories.
        /// </summary>
        public string[] ItemCategories { get; set; } = new string[] { };
        /// <summary>
        /// Cached list of cbs packs categories.
        /// </summary>
        public string[] PackCategories { get; set; } = new string[] { };
        /// <summary>
        /// Cached list of cbs lootbox categories.
        /// </summary>
        public string[] LootboxCategories { get; set; } = new string[] { };

        private IFabItems FabItems { get; set; }
        private IAuth Auth { get; set; }
        private IProfile Profile { get; set; }
        private ICBSInAppPurchase InAppPurchase { get; set; }

        protected override void Init()
        {
            FabItems = FabExecuter.Get<FabItems>();
            Auth = Get<CBSAuthModule>();
            Profile = Get<CBSProfileModule>();
            InAppPurchase = Get<CBSInAppPurchaseModule>();

            Auth.OnLoginEvent += OnPlayerLogined;
        }

        // API calls

        /// <summary>
        /// Updates the state of all items from the Playfab database(include recipes, upgrades, categories). Do not use unnecessarily. This method is called at login.
        /// </summary>
        /// <param name="result"></param>
        public void FetchAll(Action<CBSFetchAllResult> result)
        {
            // get all items
            FabItems.FetchItems(onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    var callback = new CBSFetchAllResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(callback);
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionFetchItemsResult>();
                    var itemsResult = functionResult.ItemsResult;
                    var recipes = functionResult.Recipes;
                    var upgrades = functionResult.Upgrades;
                    var lootboxTable = functionResult.LootboxTable;
                    var categories = functionResult.Categories;

                    ParseMetaData(recipes, upgrades, lootboxTable);
                    ParseItems(itemsResult);
                    ParseCategories(categories);
                    // generate callback
                    var callback = new CBSFetchAllResult
                    {
                        IsSuccess = true,
                        Items = AllItems,
                        Packs = AllPacks,
                        Lootboxes = AllLootboxes,
                        Recipes = Recipes,
                        Upgrades = Upgrades,
                        ItemsCategories = ItemCategories,
                        PacksCategories = PackCategories,
                        LootboxCategories = LootboxCategories
                    };
                    result?.Invoke(callback);
                }
            }, onError =>
            {
                var callback = new CBSFetchAllResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Get all categories by specific items type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="result"></param>
        public void GetCategories(ItemType type, Action<CBSGetCategoriesResult> result)
        {
            FabItems.GetCategories(onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    var callback = new CBSGetCategoriesResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(callback);
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetCategoriesResult>();
                    var categoriesData = functionResult.CategoriesData;
                    ParseCategories(categoriesData);
                    result?.Invoke(new CBSGetCategoriesResult
                    {
                        IsSuccess = true,
                        Categories = GetCategoryFromType(type)
                    });
                }
            }, onFailed =>
            {
                var callback = new CBSGetCategoriesResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Get all items by type or category.
        /// </summary>
        /// <param name="result"></param>
        public void GetCBSItems(CBSGetItemsRequest request, Action<CBSGetItemsResult> result)
        {
            var type = request.ItemType;
            var category = request.SpecificCategory;
            FabItems.GetItems(type, category, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    var callback = new CBSGetItemsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(callback);
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetItemsResult>();
                    var fabItems = functionResult.ItemsResult.Catalog;
                    var items = type == ItemType.ITEMS ? fabItems.Select(x => x.ToCBSItem(Recipes.GetRecipe(x.ItemId), Upgrades.GetUpdgrades(x.ItemId))).ToList() : null;
                    var packs = type == ItemType.PACKS ? fabItems.Select(x => x.ToCBSPack()).ToList() : null;
                    var lootboxes = type == ItemType.LOOT_BOXES ? fabItems.Select(x => x.ToCBSLootbox(LootboxTable)).ToList() : null;

                    var callback = new CBSGetItemsResult
                    {
                        IsSuccess = true,
                        Items = items,
                        Packs = packs,
                        Lootboxes = lootboxes
                    };
                    result?.Invoke(callback);
                }
            }, onFailed =>
            {
                var callback = new CBSGetItemsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Get specific cbs item information by id.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void GetCBSItemByID(string itemID, Action<CBSGetItemResult> result)
        {
            FabItems.GetItemByID(itemID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetItemResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetItemByIDResult>();
                    var fabItem = functionResult.FabItem;

                    result?.Invoke(new CBSGetItemResult
                    {
                        IsSuccess = true,
                        Item = fabItem.IsItem() ? fabItem.ToCBSItem(Recipes.GetRecipe(fabItem.ItemId), Upgrades.GetUpdgrades(fabItem.ItemId)) : null,
                        Lootbox = fabItem.IsLootbox() ? fabItem.ToCBSLootbox(LootboxTable) : null,
                        Pack = fabItem.IsPack() ? fabItem.ToCBSPack() : null
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Purchase item by id. The currency will be debited automatically and the item will be added to the inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="currencyCode"></param>
        /// <param name="currencyValue"></param>
        /// <param name="result"></param>
        public void PurchaseCBSItemWithCurrency(string itemID, string currencyCode, int currencyValue, Action<CBSPurchaseItemWithCurrencyResult> result)
        {
            var profileID = Profile.ProfileID;
            var cbsItem = GetFromCache(itemID);
            var itemType = cbsItem.Type;

            var dataRequest = new FabPurchaseRequest
            {
                ItemID = itemID,
                CurrencyCode = currencyCode,
                CurrencyValue = currencyValue,
                ProfileID = profileID,
                ItemType = itemType
            };

            FabItems.PurchaseItem(dataRequest, (purchaseResult, postResult, cbsError) =>
            {
                if (cbsError != null)
                {
                    result?.Invoke(new CBSPurchaseItemWithCurrencyResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var itemInstances = purchaseResult.Items;
                    var inventoryItems = itemInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                    var grantedCurrencies = postResult?.PurchasedCurrencies;
                    var callback = new CBSPurchaseItemWithCurrencyResult
                    {
                        IsSuccess = true,
                        ItemID = itemID,
                        PurchasedInstances = inventoryItems,
                        PriceCode = currencyCode,
                        PriceValue = currencyValue,
                        PurchasedCurrencies = grantedCurrencies
                    };
                    result?.Invoke(callback);
                    OnItemPurchased?.Invoke(callback);
                    // send request to inventory change
                    Get<CBSInventoryModule>().AddRequest(inventoryItems);
                    // send request to currency change
                    Get<CBSCurrencyModule>().ChangeRequest(currencyCode);
                    if (grantedCurrencies != null)
                    {
                        Get<CBSCurrencyModule>().ChangeRequest(grantedCurrencies.Select(x => x.Key).ToArray());
                    }
                }
            }, onError =>
            {
                var callback = new CBSPurchaseItemWithCurrencyResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Purchase item with Unity IAP module. Currently working with iOS/Android only
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void PurchaseCBSItemWithRealMoney(string itemID, Action<CBSPurchaseItemWithRealMoneyResult> result)
        {
            InAppPurchase.PurchaseItem(itemID, CatalogKeys.ItemsCatalogID, onPurchase =>
            {
                if (onPurchase.Error != null)
                {
                    result?.Invoke(new CBSPurchaseItemWithRealMoneyResult
                    {
                        IsSuccess = false,
                        Error = onPurchase.Error
                    });
                }
                else
                {
                    var grantedItems = onPurchase.GrantedItems;
                    var grantedCurrencies = onPurchase.GrantedCurrencies;
                    var transactionID = onPurchase.TransactionID;
                    var profileID = onPurchase.ProfileID;
                    var purshasedItemID = onPurchase.ItemID;

                    Get<CBSInventoryModule>().AddRequest(grantedItems);
                    if (grantedCurrencies != null)
                    {
                        Get<CBSCurrencyModule>().ChangeRequest(grantedCurrencies.Select(x => x.Key).ToArray());
                    }

                    var resultObject = new CBSPurchaseItemWithRealMoneyResult
                    {
                        IsSuccess = true,
                        ProfileID = profileID,
                        ItemID = itemID,
                        TransactionID = transactionID,
                        PurchasedInstances = grantedItems,
                        PurchasedCurrencies = grantedCurrencies
                    };
                    OnItemPurchasedWithRealMoney?.Invoke(resultObject);
                    result?.Invoke(resultObject);
                }
            });
        }

        /// <summary>
        /// Add item to current auth user. The item automatically goes into inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void GrantItem(string itemID, Action<CBSGrantItemsResult> result)
        {
            var profileID = Profile.ProfileID;
            var itemsIDs = new string[] { itemID };
            InternalGrantItemsToProfile(profileID, itemsIDs, result);
        }

        /// <summary>
        /// Add items to current auth user. The items automatically goes into inventory.
        /// </summary>
        /// <param name="itemsIDs"></param>
        /// <param name="result"></param>
        public void GrantItems(string[] itemsIDs, Action<CBSGrantItemsResult> result)
        {
            var profileID = Profile.ProfileID;
            InternalGrantItemsToProfile(profileID, itemsIDs, result);
        }

        /// <summary>
        /// Add item to profile by id. The item automatically goes into inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void GrantItemToProfile(string profileID, string itemID, Action<CBSGrantItemsResult> result)
        {
            var itemsIDs = new string[] { itemID };
            InternalGrantItemsToProfile(profileID, itemsIDs, result);
        }

        /// <summary>
        /// Add items to profile by id. The item automatically goes into inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void GrantItemsToProfile(string profileID, string[] itemsIDs, Action<CBSGrantItemsResult> result)
        {
            InternalGrantItemsToProfile(profileID, itemsIDs, result);
        }

        /// <summary>
        /// Get item from cached dictionary
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public CBSBaseItem GetFromCache(string itemID)
        {
            return GetFromDictionary(itemID);
        }

        // internal
        internal bool IsItemsFromPack(string[] itemsIDs)
        {
            return AllPacks.Select(x => x.ItemID).Intersect(itemsIDs).Any();
        }

        private void InternalGrantItemsToProfile(string profileID, string[] itemsIDs, Action<CBSGrantItemsResult> result)
        {
            var authProfileID = Profile.ProfileID;
            var containPack = IsItemsFromPack(itemsIDs);
            FabItems.GrantItems(profileID, itemsIDs, containPack, onGrant =>
            {
                var cbsError = onGrant.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGrantItemsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGrant.GetResult<FunctionGrantItemsResult>();
                    var grantedInstances = functionResult.GrantedInstances;
                    var grantedCurrencies = functionResult.GrantedCurrencies;
                    var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();

                    var resultObject = new CBSGrantItemsResult
                    {
                        IsSuccess = true,
                        TargetID = profileID,
                        GrantedInstances = inventoryItems,
                        GrantedCurrencies = grantedCurrencies
                    };

                    if (authProfileID == profileID)
                    {
                        // send request to inventory change
                        Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        // send request to currency change
                        Get<CBSCurrencyModule>().ChangeRequest(grantedCurrencies.Select(x => x.Key).ToArray());
                        OnItemGranted?.Invoke(resultObject);
                    }
                    result?.Invoke(resultObject);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGrantItemsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        private void ParseItems(GetCatalogItemsResult result)
        {
            // parse items
            AllItems = result.Catalog.Where(x => x.IsItem()).Select(x => x.ToCBSItem(Recipes.GetRecipe(x.ItemId), Upgrades.GetUpdgrades(x.ItemId))).ToList();
            foreach (var item in AllItems)
                ItemsDictionary[item.ItemID] = item;
            // parse packs
            AllPacks = result.Catalog.Where(x => x.IsPack()).Select(x => x.ToCBSPack()).ToList();
            foreach (var item in AllPacks)
                ItemsDictionary[item.ItemID] = item;
            // parse loot boxes
            AllLootboxes = result.Catalog.Where(x => x.IsLootbox()).Select(x => x.ToCBSLootbox(LootboxTable)).ToList();
            foreach (var item in AllLootboxes)
                ItemsDictionary[item.ItemID] = item;
        }

        private void ParseCategories(Dictionary<string, string> data)
        {
            if (data == null)
                return;
            if (data.ContainsKey(TitleKeys.ItemsCategoriesKey))
            {
                var rawData = data[TitleKeys.ItemsCategoriesKey];
                var categoryObject = JsonPlugin.FromJson<Categories>(rawData);
                ItemCategories = categoryObject.List.ToArray();
            }
            if (data.ContainsKey(TitleKeys.PackCategoriesKey))
            {
                var rawData = data[TitleKeys.PackCategoriesKey];
                var categoryObject = JsonPlugin.FromJson<Categories>(rawData);
                PackCategories = categoryObject.List.ToArray();
            }
            if (data.ContainsKey(TitleKeys.LootboxesCategoriesKey))
            {
                var rawData = data[TitleKeys.LootboxesCategoriesKey];
                var categoryObject = JsonPlugin.FromJson<Categories>(rawData);
                LootboxCategories = categoryObject.List.ToArray();
            }
        }

        internal CBSBaseItem GetFromDictionary(string itemID)
        {
            try
            {
                return ItemsDictionary[itemID];
            }
            catch
            {
                return null;
            }
        }

        internal void ParseMetaData(CBSRecipeContainer recipes, CBSItemUpgradesContainer upgrades, CBSLootboxTable lootboxTable)
        {
            Recipes = recipes;
            Upgrades = upgrades;
            LootboxTable = lootboxTable;
        }

        private string[] GetCategoryFromType(ItemType type)
        {
            if (type == ItemType.ITEMS)
                return ItemCategories;
            else if (type == ItemType.PACKS)
                return PackCategories;
            else if (type == ItemType.LOOT_BOXES)
                return LootboxCategories;
            return new string[] { };
        }

        // events
        private void OnPlayerLogined(CBSLoginResult loginResult)
        {
            var itemsResult = loginResult.ItemsResult;
            var categoriesData = loginResult.ItemsCategoryData;

            if (itemsResult != null)
            {
                ParseItems(itemsResult);
            }
            if (categoriesData != null)
            {
                ParseCategories(categoriesData);
            }
        }

        protected override void OnLogout()
        {
            AllItems = new List<CBSItem>();
            AllPacks = new List<CBSItemPack>();
            AllLootboxes = new List<CBSLootbox>();

            ItemsDictionary = new Dictionary<string, CBSBaseItem>();

            ItemCategories = new string[] { };
            PackCategories = new string[] { };
            LootboxCategories = new string[] { };

            //Auth.OnLoginEvent -= OnPlayerLogined;
        }
    }
}