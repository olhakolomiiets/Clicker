using CBS.Models;
using System;

namespace CBS
{
    public interface IStore
    {
        /// <summary>
        /// Notify when item was purchased from Store with currencies.
        /// </summary>
        event Action<CBSPurchaseStoreItemResult> OnItemPurchased;

        /// <summary>
        /// Notify when item was purchased from Store with real money
        /// </summary>
        event Action<CBSPurchaseStoreItemWithRMResult> OnItemPurchasedWithRM;

        /// <summary>
        /// Get all stores available for player.
        /// </summary>
        /// <param name="result"></param>
        void GetStores(Action<CBSGetStoresResult> result);

        /// <summary>
        /// Get all store title available for player. Get short stores information without items.
        /// </summary>
        /// <param name="result"></param>
        void GetStoreTitles(Action<CBSGetStoreTitlesResult> result);

        /// <summary>
        /// Get store by id if avalilable for player.
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="result"></param>
        void GetStoreByID(string storeID, Action<CBSGetStoreResult> result);

        /// <summary>
        /// Get specific item information from store.
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void GetStoreItemByID(string storeID, string itemID, Action<CBSGetStoreItemResult> result);

        /// <summary>
        /// Purchase store item with currencies.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="storeID"></param>
        /// <param name="currencyCode"></param>
        /// <param name="currencyValue"></param>
        /// <param name="result"></param>
        void PurchaseStoreItem(string itemID, string storeID, string currencyCode, int currencyValue, Action<CBSPurchaseStoreItemResult> result);

        /// <summary>
        /// Purchase store item with real money. Required "CBSIAP" module enabled
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="storeID"></param>
        /// <param name="result"></param>
        void PurchaseStoreItemWithRealMoney(string itemID, string storeID, Action<CBSPurchaseStoreItemWithRMResult> result);

        /// <summary>
        /// Revoke quantity limitation of item for player.
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void RevokeItemStoreLimitation(string storeID, string itemID, Action<CBSRevokeLimitationResult> result);

        /// <summary>
        /// Get all special offers available for profile. Included "global offers" and "profile offers".
        /// </summary>
        /// <param name="result"></param>
        void GetSpecialOffers(Action<CBSGetSpecialOffersResult> result);

        /// <summary>
        /// Grant special offer for profile. Required itemID from "Profile Special offers" section.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void GrantSpecialOfferToProfile(string itemID, Action<CBSSpecialOfferResult> result);
    }
}
