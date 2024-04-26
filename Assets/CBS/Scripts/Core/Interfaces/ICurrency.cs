using CBS.Models;
using System;
using System.Collections.Generic;

namespace CBS
{
    public interface ICurrency
    {
        /// <summary>
        /// Notifies when the state of the game currency has been updated.
        /// </summary>
        event Action<CBSCurrency> OnCurrencyUpdated;

        /// <summary>
        /// Get last cached currency data.
        /// </summary>
        Dictionary<string, CBSCurrency> CacheCurrencies { get; }

        /// <summary>
        /// Get currencies of current profile
        /// </summary>
        /// <param name="result"></param>
        void GetProfileCurrencies(Action<CBSGetCurrenciesResult> result = null);

        /// <summary>
        /// Get currencies by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void GetProfileCurrencies(string profileID, Action<CBSGetCurrenciesResult> result);

        /// <summary>
        /// Add currency to the current user.
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="currencyCode"></param>
        /// <param name="result"></param>
        void AddCurrencyToProfile(string code, int amount, Action<CBSUpdateCurrencyResult> result = null);

        /// <summary>
        /// Subtract game currency by profile ID
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="currency"></param>
        /// <param name="result"></param>
        void SubtractCurrencyFromProfile(string profileID, string code, int amount, Action<CBSUpdateCurrencyResult> result = null);

        /// <summary>
        /// Add currency to user by profile ID.
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="currencyCode"></param>
        /// <param name="result"></param>
        void AddCurrencyToProfile(string profileID, string code, int amount, Action<CBSUpdateCurrencyResult> result = null);

        /// <summary>
        /// Subtract game currency from current user.
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="currency"></param>
        /// <param name="result"></param>
        void SubtractCurrencyFromProfile(string code, int amount, Action<CBSUpdateCurrencyResult> result = null);

        /// <summary>
        /// Get all currency packs.
        /// </summary>
        /// <param name="result"></param>
        void GetCurrenciesPacks(Action<CBSGetCurrenciesPacksResult> result);

        /// <summary>
        /// Get all currency packs by specific tag
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="result"></param>
        void GetPacksByTag(string tag, Action<CBSGetCurrenciesPacksResult> result);

        /// <summary>
        /// Grant currency pack to current user.
        /// </summary>
        /// <param name="pack"></param>
        /// <param name="result"></param>
        void GrantCurrencyPack(string packID, Action<CBSGrandPackResult> result);

        /// <summary>
        /// Purchase pack with real money using Unity IAP
        /// </summary>
        /// <param name="packID"></param>
        /// <param name="result"></param>
        void PurchasePackWithRealMoney(string packID, Action<CBSPurchasePackWithRealMoneyResult> result);
    }
}
