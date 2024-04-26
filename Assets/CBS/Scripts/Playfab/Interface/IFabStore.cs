using CBS.Models;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public interface IFabStore
    {
        void GetAllStores(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetAllStoreTitles(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetStoreByID(string profileID, string storeID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetStoreItemByID(string profileID, string storeID, string itemID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void PrePurchaseValidation(string profileID, string storeID, string itemID, Action<ExecuteFunctionResult> onValidate, Action<PlayFabError> onFailed);

        void PurchaseItem(FabPurchaseRequest purchaseRequest, Action<PurchaseItemResult, FunctionPostStorePurchaseResult, CBSError> onPurchase, Action<PlayFabError> onFailed);

        void RevokeItemLimitation(string profileID, string storeID, string itemID, Action<ExecuteFunctionResult> onRevoke, Action<PlayFabError> onFailed);

        void GetSpecialOffers(string profileID, Action<ExecuteFunctionResult> onRevoke, Action<PlayFabError> onFailed);

        void GrantSpecialOfferToProfile(string profileID, string itemID, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed);
    }
}
