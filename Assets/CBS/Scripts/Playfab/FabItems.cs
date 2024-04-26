using CBS.Models;
using CBS.Utils;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabItems : FabExecuter, IFabItems
    {
        public void FetchItems(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.FetchItemsMethod
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetItems(ItemType type, string category, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetFabItemsMethod,
                FunctionParameter = new FunctionGetItemsRequest
                {
                    ItemType = type,
                    SpecificCategory = category
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetItemByID(string itemID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetFabItemByIDMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ID = itemID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetCategories(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetItemsCategoriesMethod
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void PurchaseItem(FabPurchaseRequest purchaseRequest, Action<PurchaseItemResult, FunctionPostPurchaseResult, CBSError> onPurchase, Action<PlayFabError> onFailed)
        {
            var profileID = purchaseRequest.ProfileID;
            var itemID = purchaseRequest.ItemID;
            var currencyCode = purchaseRequest.CurrencyCode;
            var currencyValue = purchaseRequest.CurrencyValue;
            var itemType = purchaseRequest.ItemType;

            var request = new PurchaseItemRequest
            {
                ItemId = itemID,
                VirtualCurrency = currencyCode,
                Price = currencyValue,
                CatalogVersion = CatalogKeys.ItemsCatalogID
            };
            PlayFabClientAPI.PurchaseItem(request, purchaseProcces =>
            {
                if (itemType == ItemType.PACKS)
                {
                    var postRequest = new ExecuteFunctionRequest
                    {
                        FunctionName = AzureFunctions.PostPurchaseProccessMethod,
                        FunctionParameter = new FunctionPostPurchaseRequest
                        {
                            ProfileID = profileID,
                            ItemID = itemID
                        }
                    };
                    PlayFabCloudScriptAPI.ExecuteFunction(postRequest, onProccess =>
                    {
                        var cbsError = onProccess.GetCBSError();
                        var functionResult = onProccess.GetResult<FunctionPostPurchaseResult>();
                        onPurchase?.Invoke(purchaseProcces, functionResult, cbsError);
                    }, onFailed);
                }
                else
                {
                    onPurchase?.Invoke(purchaseProcces, null, null);
                }
            }, onFailed);
        }

        public void GrantItems(string profileID, string[] itemsIDs, bool containPack, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GrantItemsToProfileMethod,
                FunctionParameter = new GrantItemsToProfileRequest
                {
                    ProfileID = profileID,
                    ItemsIDs = itemsIDs,
                    ContainPack = containPack,
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGrant, onFailed);
        }
    }
}
