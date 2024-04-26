using CBS.Models;
using System;
using System.Collections.Generic;

namespace CBS
{
    public interface ICBSItems
    {
        /// <summary>
        /// Notify when cbs item was purchased.
        /// </summary>
        event Action<CBSPurchaseItemWithCurrencyResult> OnItemPurchased;
        /// <summary>
        /// Notify when cbs item was purchased with real money.
        /// </summary>
        event Action<CBSPurchaseItemWithRealMoneyResult> OnItemPurchasedWithRealMoney;
        /// <summary>
        /// Notify when items was granted to user.
        /// </summary>
        event Action<CBSGrantItemsResult> OnItemGranted;

        /// <summary>
        /// Cached list of all cbs items available for playfab
        /// </summary>
        List<CBSItem> AllItems { get; }
        /// <summary>
        /// Cached list of all cbs packs available for playfab
        /// </summary>
        List<CBSItemPack> AllPacks { get; }
        /// <summary>
        /// Cached list of all cbs lootboxes available for playfab
        /// </summary>
        List<CBSLootbox> AllLootboxes { get; }
        /// <summary>
        /// Cached recipes data from playfab.
        /// </summary>
        CBSRecipeContainer Recipes { get; }
        /// <summary>
        /// Cached upgrades data from playfab.
        /// </summary>
        CBSItemUpgradesContainer Upgrades { get; }
        /// <summary>
        /// Dictionary of all cbs items (include packs and lutboxes).
        /// </summary>
        Dictionary<string, CBSBaseItem> ItemsDictionary { get; }

        /// <summary>
        /// Cached list of cbs items categories.
        /// </summary>
        string[] ItemCategories { get; }
        /// <summary>
        /// Cached list of cbs packs categories.
        /// </summary>
        string[] PackCategories { get; }
        /// <summary>
        /// Cached list of cbs lootbox categories.
        /// </summary>
        string[] LootboxCategories { get; }

        /// <summary>
        /// Updates the state of all items from the Playfab database(include recipes, upgrades, categories). Do not use unnecessarily. This method is called at login.
        /// </summary>
        /// <param name="result"></param>
        void FetchAll(Action<CBSFetchAllResult> result);

        /// <summary>
        /// Get all categories by specific items type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="result"></param>
        void GetCategories(ItemType type, Action<CBSGetCategoriesResult> result);
        /// <summary>
        /// Get all items by type or categoty.
        /// </summary>
        /// <param name="result"></param>
        void GetCBSItems(CBSGetItemsRequest request, Action<CBSGetItemsResult> result);
        /// <summary>
        /// Get specific cbs item information by id.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void GetCBSItemByID(string itemID, Action<CBSGetItemResult> result);
        /// <summary>
        /// Purchase item by id. The currency will be debited automatically and the item will be added to the inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="currencyCode"></param>
        /// <param name="currencyValue"></param>
        /// <param name="result"></param>
        void PurchaseCBSItemWithCurrency(string itemID, string currencyCode, int currencyValue, Action<CBSPurchaseItemWithCurrencyResult> result);
        /// <summary>
        /// Purchase item with Unity IAP module. Currently working with iOS/Android only
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void PurchaseCBSItemWithRealMoney(string itemID, Action<CBSPurchaseItemWithRealMoneyResult> result);
        /// <summary>
        /// Add item to to current auth user. The item automatically goes into inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void GrantItem(string itemID, Action<CBSGrantItemsResult> result);
        /// <summary>
        /// Add items to current auth user. The items automatically goes into inventory.
        /// </summary>
        /// <param name="itemsID"></param>
        /// <param name="result"></param>
        void GrantItems(string[] itemsID, Action<CBSGrantItemsResult> result);

        /// <summary>
        /// Add item to profile by id. The item automatically goes into inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void GrantItemToProfile(string profileID, string itemID, Action<CBSGrantItemsResult> result);

        /// <summary>
        /// Add items to profile by id. The item automatically goes into inventory.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="itemsIDs"></param>
        /// <param name="result"></param>
        void GrantItemsToProfile(string profileID, string[] itemsIDs, Action<CBSGrantItemsResult> result);

        /// <summary>
        /// Get item from cached dictionary
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        CBSBaseItem GetFromCache(string itemID);
    }
}
