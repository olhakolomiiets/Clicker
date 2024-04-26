using CBS.Models;
using CBS.Playfab;
using CBS.Scriptable;
using CBS.Utils;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSCurrencyModule : CBSModule, ICurrency
    {
        public event Action<CBSCurrency> OnCurrencyUpdated;

        private readonly string CatalogID = CatalogKeys.CurrencyCatalogID;

        public Dictionary<string, CBSCurrency> CacheCurrencies { get; private set; }

        private IFabCurrency FabCurrency { get; set; }
        private IProfile Profile { get; set; }
        private ICBSInAppPurchase InAppPurchase { get; set; }
        private IAuth Auth { get; set; }
        private AuthData AuthData { get; set; }

        protected override void Init()
        {
            Profile = Get<CBSProfileModule>();
            InAppPurchase = Get<CBSInAppPurchaseModule>();
            FabCurrency = FabExecuter.Get<FabCurrency>();
            AuthData = CBSScriptable.Get<AuthData>();
            Auth = Get<CBSAuthModule>();
            Auth.OnLoginEvent += OnLoginSuccess;
        }

        // API calls

        /// <summary>
        /// Get currencies of current profile
        /// </summary>
        /// <param name="result"></param>
        public void GetProfileCurrencies(Action<CBSGetCurrenciesResult> result)
        {
            string profileID = Profile.ProfileID;
            FabCurrency.GetProfileCurrencies(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSGetCurrenciesResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionCurrenciesResult>();
                    var currencies = functionResult.Currencies;
                    // parse and cache current currencies
                    ParseCurrencies(currencies);

                    var callback = new CBSGetCurrenciesResult
                    {
                        IsSuccess = true,
                        TargetID = profileID,
                        Currencies = currencies
                    };
                    result?.Invoke(callback);
                }
            },
            onError =>
            {
                var callback = new CBSGetCurrenciesResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Get currencies by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        public void GetProfileCurrencies(string profileID, Action<CBSGetCurrenciesResult> result)
        {
            FabCurrency.GetProfileCurrencies(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSGetCurrenciesResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionCurrenciesResult>();
                    var currencies = functionResult.Currencies;

                    var callback = new CBSGetCurrenciesResult
                    {
                        IsSuccess = true,
                        TargetID = profileID,
                        Currencies = currencies
                    };
                    result?.Invoke(callback);
                }
            },
            onError =>
            {
                var callback = new CBSGetCurrenciesResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Add currency to the current profile.
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="currencyCode"></param>
        /// <param name="result"></param>
        public void AddCurrencyToProfile(string code, int amount, Action<CBSUpdateCurrencyResult> result = null)
        {
            var profileID = Profile.ProfileID;
            AddCurrency(profileID, code, amount, result);
        }

        /// <summary>
        /// Subtract game currency from current profile.
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="currency"></param>
        /// <param name="result"></param>
        public void SubtractCurrencyFromProfile(string code, int amount, Action<CBSUpdateCurrencyResult> result = null)
        {
            var profileID = Profile.ProfileID;
            SubtractCurrency(profileID, code, amount, result);
        }

        /// <summary>
        /// Add currency to user by profile ID.
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="currencyCode"></param>
        /// <param name="result"></param>
        public void AddCurrencyToProfile(string profileID, string code, int amount, Action<CBSUpdateCurrencyResult> result = null)
        {
            AddCurrency(profileID, code, amount, result);
        }

        /// <summary>
        /// Subtract game currency by profile ID
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="currency"></param>
        /// <param name="result"></param>
        public void SubtractCurrencyFromProfile(string profileID, string code, int amount, Action<CBSUpdateCurrencyResult> result = null)
        {
            SubtractCurrency(profileID, code, amount, result);
        }

        /// <summary>
        /// Get all currencies packs.
        /// </summary>
        /// <param name="result"></param>
        public void GetCurrenciesPacks(Action<CBSGetCurrenciesPacksResult> result)
        {
            GetPacksByTag(string.Empty, result);
        }

        /// <summary>
        /// Get all currency packs by specific tag
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="result"></param>
        public void GetPacksByTag(string tag, Action<CBSGetCurrenciesPacksResult> result)
        {
            FabCurrency.GetCurrenciesPacks(tag, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    var callback = new CBSGetCurrenciesPacksResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(callback);
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionCatalogItemsResult>();
                    var items = functionResult.Items;
                    var hasTag = !string.IsNullOrEmpty(tag);
                    var packs = items.Select(x => new CBSCurrencyPack(x)).ToList();
                    if (hasTag)
                        packs = packs.Where(x => x.Tag == tag).ToList();
                    result?.Invoke(new CBSGetCurrenciesPacksResult
                    {
                        IsSuccess = true,
                        Packs = packs
                    });
                }
            }, onError =>
            {
                var callback = new CBSGetCurrenciesPacksResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Grant currency pack to current profile.
        /// </summary>
        /// <param name="pack"></param>
        /// <param name="result"></param>
        public void GrantCurrencyPack(string packID, Action<CBSGrandPackResult> result)
        {
            var profileID = Profile.ProfileID;
            FabCurrency.GrantCurrencyPack(profileID, packID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    var callback = new CBSGrandPackResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(callback);
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGrantCurrencyPackResult>();
                    var grantedProfileID = functionResult.ProfileID;
                    var grantedCurrencies = functionResult.GrantedCurrencies;
                    var grantedCurrenciesKeys = grantedCurrencies.Select(x => x.Key).ToArray();
                    ChangeRequest(grantedCurrenciesKeys);
                    result?.Invoke(new CBSGrandPackResult
                    {
                        IsSuccess = true,
                        ProfileID = grantedProfileID,
                        GrantedCurrencies = grantedCurrencies
                    });
                }
            }, onError =>
            {
                var callback = new CBSGrandPackResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Purchase pack with real money using Unity IAP
        /// </summary>
        /// <param name="packID"></param>
        /// <param name="result"></param>
        public void PurchasePackWithRealMoney(string packID, Action<CBSPurchasePackWithRealMoneyResult> result)
        {
            InAppPurchase.PurchaseItem(packID, CatalogID, onPurchase =>
            {
                if (onPurchase.IsSuccess)
                {
                    var grantedProfileID = onPurchase.ProfileID;
                    var grantedCurrencies = onPurchase.GrantedCurrencies;
                    var grantedCurrenciesKeys = grantedCurrencies.Select(x => x.Key).ToArray();
                    ChangeRequest(grantedCurrenciesKeys);
                    result?.Invoke(new CBSPurchasePackWithRealMoneyResult
                    {
                        IsSuccess = true,
                        TransactionID = onPurchase.TransactionID,
                        ProfileID = grantedProfileID,
                        GrantedCurrencies = grantedCurrencies
                    });
                }
                else
                {
                    result?.Invoke(new CBSPurchasePackWithRealMoneyResult
                    {
                        IsSuccess = false,
                        Error = onPurchase.Error
                    });
                }
            });
        }

        // internal
        private void AddCurrency(string profileID, string code, int amount, Action<CBSUpdateCurrencyResult> result = null)
        {
            FabCurrency.AddProfileCurrerncy(profileID, code, amount, onChange =>
            {
                var cbsError = onChange.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSUpdateCurrencyResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onChange.GetResult<FunctionChangeCurrencyResult>();
                    var updatedCurrency = functionResult.UpdatedCurrency;
                    if (profileID == Profile.ProfileID)
                        ChangeRequest(updatedCurrency.Code);
                    result?.Invoke(new CBSUpdateCurrencyResult
                    {
                        IsSuccess = true,
                        TargetID = functionResult.TargetID,
                        UpdatedCurrency = updatedCurrency,
                        BalanceChange = functionResult.BalanceChange,
                    });
                    var authProfileID = Profile.ProfileID;
                    if (authProfileID == profileID)
                        OnCurrencyUpdated?.Invoke(functionResult.UpdatedCurrency);
                }
            },
            onError =>
            {
                var callback = new CBSUpdateCurrencyResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        private void SubtractCurrency(string profileID, string code, int amount, Action<CBSUpdateCurrencyResult> result = null)
        {
            FabCurrency.SubtractProfileCurrerncy(profileID, code, amount, onChange =>
            {
                var cbsError = onChange.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSUpdateCurrencyResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onChange.GetResult<FunctionChangeCurrencyResult>();
                    var updatedCurrency = functionResult.UpdatedCurrency;
                    if (profileID == Profile.ProfileID)
                        ChangeRequest(updatedCurrency.Code);
                    result?.Invoke(new CBSUpdateCurrencyResult
                    {
                        IsSuccess = true,
                        TargetID = functionResult.TargetID,
                        UpdatedCurrency = updatedCurrency,
                        BalanceChange = functionResult.BalanceChange,
                    });
                    var authProfileID = Profile.ProfileID;
                    if (authProfileID == profileID)
                        OnCurrencyUpdated?.Invoke(functionResult.UpdatedCurrency);
                }
            },
            onError =>
            {
                var callback = new CBSUpdateCurrencyResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        internal void ParseCurrencies(Dictionary<string, CBSCurrency> currencyList)
        {
            CacheCurrencies = currencyList;
        }

        private void ParseCurrencies(GetUserInventoryResult inventory)
        {
            var cbsCurrency = new Dictionary<string, CBSCurrency>();
            var fabCurrency = inventory.VirtualCurrency ?? new Dictionary<string, int>();
            var fabRechargeCurrency = inventory.VirtualCurrencyRechargeTimes ?? new Dictionary<string, VirtualCurrencyRechargeTime>();
            var currenyCount = fabCurrency.Count;
            for (int i = 0; i < currenyCount; i++)
            {
                var fabTarget = fabCurrency.ElementAt(i);
                var key = fabTarget.Key;
                var value = fabTarget.Value;
                var rechargeable = fabRechargeCurrency.ContainsKey(key);
                var maxRecharge = rechargeable ? fabRechargeCurrency[key].RechargeMax : 0;
                DateTime? rechargeDate = rechargeable ? (DateTime?)fabRechargeCurrency[key].RechargeTime : null;
                var secondsToRecharge = rechargeable ? fabRechargeCurrency[key].SecondsToRecharge : 0;

                cbsCurrency[key] = new CBSCurrency
                {
                    Code = key,
                    Value = value,
                    Rechargeable = rechargeable,
                    MaxRecharge = maxRecharge,
                    RechargeTime = rechargeDate,
                    SecondsToRecharge = secondsToRecharge
                };
            }
            CacheCurrencies = cbsCurrency;
        }

        internal void ChangeRequest(string code)
        {
            GetProfileCurrencies(onGet =>
            {
                if (onGet.IsSuccess)
                {
                    var currencies = onGet.Currencies;
                    var currencyToChange = currencies[code];
                    OnCurrencyUpdated?.Invoke(currencyToChange);
                }
            });
        }

        internal void ChangeRequest(string[] codes)
        {
            GetProfileCurrencies(onGet =>
            {
                if (onGet.IsSuccess)
                {
                    var currencies = onGet.Currencies;
                    foreach (var code in codes)
                    {
                        OnCurrencyUpdated?.Invoke(currencies[code]);
                    }
                }
            });
        }

        protected override void OnLogout()
        {
            CacheCurrencies = null;
        }

        public CBSCurrency GetFromCache(string code)
        {
            if (CacheCurrencies.ContainsKey(code))
                return CacheCurrencies[code];
            else
                return CBSCurrency.Default(code);
        }

        // events
        private void OnLoginSuccess(CBSLoginResult result)
        {
            if (result.IsSuccess)
            {
                if (AuthData.PreloadCurrency)
                {
                    var currencies = result.Result.InfoResultPayload.UserVirtualCurrency;
                    var rechargeInfo = result.Result.InfoResultPayload.UserVirtualCurrencyRechargeTimes;
                    var currencyResult = new GetUserInventoryResult
                    {
                        VirtualCurrency = currencies,
                        VirtualCurrencyRechargeTimes = rechargeInfo
                    };
                    ParseCurrencies(currencyResult);
                }
            }
        }
    }
}
