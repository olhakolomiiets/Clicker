

using CBS.Models;
using CBS.Utils;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabStore : FabExecuter, IFabStore
    {
        public void GetAllStores(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetAllStoresMethod,
                FunctionParameter = new FunctionsGetStoresRequest
                {
                    ProfileID = profileID,
                    TimeZoneOffset = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetAllStoreTitles(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetAllStoreTitlesMethod,
                FunctionParameter = new FunctionsGetStoresRequest
                {
                    ProfileID = profileID,
                    TimeZoneOffset = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetStoreByID(string profileID, string storeID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetStoreByIDMethod,
                FunctionParameter = new FunctionsGetStoreRequest
                {
                    ProfileID = profileID,
                    StoreID = storeID,
                    TimeZoneOffset = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetStoreItemByID(string profileID, string storeID, string itemID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetStoreByIDMethod,
                FunctionParameter = new FunctionStoreItemRequest
                {
                    ProfileID = profileID,
                    StoreID = storeID,
                    ItemID = itemID,
                    TimeZoneOffset = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void PrePurchaseValidation(string profileID, string storeID, string itemID, Action<ExecuteFunctionResult> onValidate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PreStorePurchaseProccessMethod,
                FunctionParameter = new FunctionStoreItemRequest
                {
                    ProfileID = profileID,
                    StoreID = storeID,
                    ItemID = itemID,
                    TimeZoneOffset = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onValidate, onFailed);
        }

        public void PurchaseItem(FabPurchaseRequest purchaseRequest, Action<PurchaseItemResult, FunctionPostStorePurchaseResult, CBSError> onPurchase, Action<PlayFabError> onFailed)
        {
            var profileID = purchaseRequest.ProfileID;
            var itemID = purchaseRequest.ItemID;
            var currencyCode = purchaseRequest.CurrencyCode;
            var currencyValue = purchaseRequest.CurrencyValue;
            var itemType = purchaseRequest.ItemType;
            var storeID = purchaseRequest.StoreID;
            var checkLimitation = purchaseRequest.CheckLimitation;

            var request = new PurchaseItemRequest
            {
                ItemId = itemID,
                VirtualCurrency = currencyCode,
                Price = currencyValue,
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = storeID
            };

            PlayFabClientAPI.PurchaseItem(request, purchaseProcces =>
            {
                var postRequest = new ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.PostStorePurchaseProccessMethod,
                    FunctionParameter = new FunctionValidateStorePurchaseRequest
                    {
                        ProfileID = profileID,
                        ItemID = itemID,
                        StoreID = storeID,
                        IsPack = itemType == ItemType.PACKS,
                        TimeZoneOffset = DateUtils.GetZoneOffset()
                    }
                };
                PlayFabCloudScriptAPI.ExecuteFunction(postRequest, onProccess =>
                {
                    var cbsError = onProccess.GetCBSError();
                    var functionResult = onProccess.GetResult<FunctionPostStorePurchaseResult>();
                    onPurchase?.Invoke(purchaseProcces, functionResult, cbsError);
                }, onFailed);
            }, onFailed);
        }

        public void RevokeItemLimitation(string profileID, string storeID, string itemID, Action<ExecuteFunctionResult> onRevoke, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.RevokeStoreItemLimitationMethod,
                FunctionParameter = new FunctionStoreItemRequest
                {
                    ProfileID = profileID,
                    StoreID = storeID,
                    ItemID = itemID,
                    TimeZoneOffset = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onRevoke, onFailed);
        }

        public void GetSpecialOffers(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetSpecialOffersMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GrantSpecialOfferToProfile(string profileID, string itemID, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GrantSpecialOfferToProfileMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = itemID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGrant, onFailed);
        }
    }
}
