using CBS.Models;
using System;

namespace CBS
{
    public interface IClanEconomy
    {
        /// <summary>
        /// Get inventory items list of clan
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void GetClanInventory(string clanID, Action<CBSGetInventoryResult> result);

        /// <summary>
        /// Add item to clan by id. The item automatically goes into inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void GrantItemToClan(string clanID, string itemID, Action<CBSGrantItemsResult> result);

        /// <summary>
        /// Add items to clan by id. The items automatically goes into inventory.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="itemsID"></param>
        /// <param name="result"></param>
        void GrantItemsToClan(string clanID, string[] itemsID, Action<CBSGrantItemsResult> result);

        /// <summary>
        /// Get information about clan currencies
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void GetClanCurrencies(string clanID, Action<CBSGetCurrenciesResult> result);

        /// <summary>
        /// Add currency value to current clan balance
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="code"></param>
        /// <param name="amount"></param>
        /// <param name="result"></param>
        void AddCurrencyToClan(string clanID, string code, int amount, Action<CBSUpdateCurrencyResult> result = null);

        /// <summary>
        /// Subtract currency value from current clan balance.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="code"></param>
        /// <param name="amount"></param>
        /// <param name="result"></param>
        void SubtractCurrencyFromClan(string clanID, string code, int amount, Action<CBSUpdateCurrencyResult> result = null);

        /// <summary>
        /// Move item from profile inventory to clan inventory
        /// </summary>
        /// <param name="itemInstanceID"></param>
        /// <param name="result"></param>
        void TransferItemFromProfileToClan(string itemInstanceID, Action<CBSClanTransferItemResult> result);

        /// <summary>
        /// Move item from clan inventory to profile inventory
        /// </summary>
        /// <param name="itemInstanceID"></param>
        /// <param name="result"></param>
        void TransferItemFromClanToProfile(string itemInstanceID, Action<CBSClanTransferItemResult> result);
    }
}
