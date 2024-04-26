using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace CBS
{
    public class CBSStoreModule : CBSModule, IStore
    {
        /// <summary>
        /// Notify when item was purchased from Store with currencies.
        /// </summary>
        public event Action<CBSPurchaseStoreItemResult> OnItemPurchased;
        /// <summary>
        /// Notify when item was purchased from Store with real money
        /// </summary>
        public event Action<CBSPurchaseStoreItemWithRMResult> OnItemPurchasedWithRM;

        private IProfile Profile { get; set; }
        private IFabStore FabStore { get; set; }
        private ICBSItems Items { get; set; }
        private ICBSInAppPurchase InAppPurchase { get; set; }

        protected override void Init()
        {
            Profile = Get<CBSProfileModule>();
            FabStore = FabExecuter.Get<FabStore>();
            Items = Get<CBSItemsModule>();
            InAppPurchase = Get<CBSInAppPurchaseModule>();
        }

        /// <summary>
        /// Get all store title available for player. Get short stores information without items.
        /// </summary>
        /// <param name="result"></param>
        public void GetStoreTitles(Action<CBSGetStoreTitlesResult> result)
        {
            var profileID = Profile.ProfileID;
            FabStore.GetAllStoreTitles(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetStoreTitlesResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetStoreTitlesResult>();
                    var titles = functionResult.StoreTitles;
                    result.Invoke(new CBSGetStoreTitlesResult
                    {
                        IsSuccess = true,
                        StoreTitles = titles
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetStoreTitlesResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get all stores available for player.
        /// </summary>
        /// <param name="result"></param>
        public void GetStores(Action<CBSGetStoresResult> result)
        {
            var profileID = Profile.ProfileID;
            FabStore.GetAllStores(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetStoresResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetStoresResult>();
                    var stores = functionResult.Stores;
                    result.Invoke(new CBSGetStoresResult
                    {
                        IsSuccess = true,
                        Stores = stores
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetStoresResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get store by id if avalilable for player.
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="result"></param>
        public void GetStoreByID(string storeID, Action<CBSGetStoreResult> result)
        {
            var profileID = Profile.ProfileID;
            FabStore.GetStoreByID(profileID, storeID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetStoreResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<CBSStore>();
                    var store = functionResult;
                    result.Invoke(new CBSGetStoreResult
                    {
                        IsSuccess = true,
                        Store = store
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetStoreResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get specific item information from store.
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void GetStoreItemByID(string storeID, string itemID, Action<CBSGetStoreItemResult> result)
        {
            var profileID = Profile.ProfileID;
            FabStore.GetStoreItemByID(profileID, storeID, itemID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetStoreItemResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<CBSStoreItem>();
                    var item = functionResult;
                    result.Invoke(new CBSGetStoreItemResult
                    {
                        IsSuccess = true,
                        Item = item
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetStoreItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Purchase store item with currencies.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="storeID"></param>
        /// <param name="currencyCode"></param>
        /// <param name="currencyValue"></param>
        /// <param name="result"></param>
        public void PurchaseStoreItem(string itemID, string storeID, string currencyCode, int currencyValue, Action<CBSPurchaseStoreItemResult> result)
        {
            InternalPurchaseCBSItemWithCurrency(itemID, storeID, currencyCode, currencyValue, result);
        }

        /// <summary>
        /// Purchase store item with real money. Required "CBSIAP" module enabled
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="storeID"></param>
        /// <param name="result"></param>
        public void PurchaseStoreItemWithRealMoney(string itemID, string storeID, Action<CBSPurchaseStoreItemWithRMResult> result)
        {
            InternalPurchaseStoreItemWithRealMoney(itemID, storeID, result);
        }

        /// <summary>
        /// Revoke quantity limitation of item for player.
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void RevokeItemStoreLimitation(string storeID, string itemID, Action<CBSRevokeLimitationResult> result)
        {
            var profileID = Profile.ProfileID;
            FabStore.RevokeItemLimitation(profileID, storeID, itemID, onRevoke =>
            {
                var cbsError = onRevoke.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSRevokeLimitationResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onRevoke.GetResult<FunctionRevokeLimitationResult>();

                    result?.Invoke(new CBSRevokeLimitationResult
                    {
                        IsSuccess = true,
                        ItemID = functionResult.ItemID,
                        StoreID = functionResult.StoreID
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSRevokeLimitationResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get all special offers available for profile. Included "global offers" and "profile offers".
        /// </summary>
        /// <param name="result"></param>
        public void GetSpecialOffers(Action<CBSGetSpecialOffersResult> result)
        {
            var profileID = Profile.ProfileID;
            FabStore.GetSpecialOffers(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetSpecialOffersResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionSpecialOffersResult>();
                    var offers = functionResult.Offers;
                    result?.Invoke(new CBSGetSpecialOffersResult
                    {
                        IsSuccess = true,
                        Offers = offers
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetSpecialOffersResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Grant special offer for profile. Required itemID from "Profile Special offers" section.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void GrantSpecialOfferToProfile(string itemID, Action<CBSSpecialOfferResult> result)
        {
            var profileID = Profile.ProfileID;
            FabStore.GrantSpecialOfferToProfile(profileID, itemID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSSpecialOfferResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGrantSpecialOfferResult>();
                    var offer = functionResult.Offer;
                    result?.Invoke(new CBSSpecialOfferResult
                    {
                        IsSuccess = true,
                        Offer = offer
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSSpecialOfferResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        // internal
        private void InternalPurchaseCBSItemWithCurrency(string itemID, string storeID, string currencyCode, int currencyValue, Action<CBSPurchaseStoreItemResult> result)
        {
            var profileID = Profile.ProfileID;
            var cbsItem = Items.GetFromCache(itemID);
            var itemType = cbsItem.Type;

            FabStore.PrePurchaseValidation(profileID, storeID, itemID, onValidate =>
            {
                var preCBSError = onValidate.GetCBSError();
                if (preCBSError != null)
                {
                    var callback = new CBSPurchaseStoreItemResult
                    {
                        IsSuccess = false,
                        Error = preCBSError
                    };
                    result?.Invoke(callback);
                }
                else
                {
                    var dataRequest = new FabPurchaseRequest
                    {
                        ItemID = itemID,
                        StoreID = storeID,
                        CurrencyCode = currencyCode,
                        CurrencyValue = currencyValue,
                        ProfileID = profileID,
                        ItemType = itemType
                    };

                    FabStore.PurchaseItem(dataRequest, (purchaseResult, postResult, cbsError) =>
                    {
                        if (cbsError != null)
                        {
                            result?.Invoke(new CBSPurchaseStoreItemResult
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
                            var callback = new CBSPurchaseStoreItemResult
                            {
                                IsSuccess = true,
                                ItemID = itemID,
                                StoreID = storeID,
                                PurchasedInstances = inventoryItems,
                                PriceCode = currencyCode,
                                PriceValue = currencyValue,
                                PurchasedCurrencies = grantedCurrencies,
                                LimitationInfo = postResult?.Limitation
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
                        var callback = new CBSPurchaseStoreItemResult
                        {
                            IsSuccess = false,
                            Error = CBSError.FromTemplate(onError)
                        };
                        result?.Invoke(callback);
                    });
                }
            },
            onFailed =>
            {
                var callback = new CBSPurchaseStoreItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                };
                result?.Invoke(callback);
            });
        }

        private void InternalPurchaseStoreItemWithRealMoney(string itemID, string storeID, Action<CBSPurchaseStoreItemWithRMResult> result)
        {
            var profileID = Profile.ProfileID;
            FabStore.PrePurchaseValidation(profileID, storeID, itemID, onValidate =>
            {
                var preCBSError = onValidate.GetCBSError();
                if (preCBSError != null)
                {
                    var callback = new CBSPurchaseStoreItemWithRMResult
                    {
                        IsSuccess = false,
                        Error = preCBSError
                    };
                    result?.Invoke(callback);
                }
                else
                {
                    InAppPurchase.PurchaseItem(itemID, CatalogKeys.ItemsCatalogID, storeID, onPurchase =>
                    {
                        if (onPurchase.Error != null)
                        {
                            result?.Invoke(new CBSPurchaseStoreItemWithRMResult
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
                            profileID = onPurchase.ProfileID;
                            var purshasedItemID = onPurchase.ItemID;
                            var limitation = onPurchase.LimitationInfo;

                            if (grantedCurrencies != null)
                            {
                                Get<CBSCurrencyModule>().ChangeRequest(grantedCurrencies.Select(x => x.Key).ToArray());
                            }
                            Get<CBSInventoryModule>().AddRequest(grantedItems);

                            var resultObject = new CBSPurchaseStoreItemWithRMResult
                            {
                                IsSuccess = true,
                                ProfileID = profileID,
                                ItemID = itemID,
                                StoreID = storeID,
                                TransactionID = transactionID,
                                PurchasedInstances = grantedItems,
                                PurchasedCurrencies = grantedCurrencies,
                                LimitationInfo = limitation
                            };
                            OnItemPurchasedWithRM?.Invoke(resultObject);
                            result?.Invoke(resultObject);
                        }
                    });
                }
            },
            onFailed =>
            {
                var callback = new CBSPurchaseStoreItemWithRMResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                };
                result?.Invoke(callback);
            });
        }
    }
}
