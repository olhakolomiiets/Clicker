using CBS.Models;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public interface IFabItems
    {
        void FetchItems(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetItems(ItemType type, string category, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetItemByID(string itemID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetCategories(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void PurchaseItem(FabPurchaseRequest purchaseRequest, Action<PurchaseItemResult, FunctionPostPurchaseResult, CBSError> onPurchase, Action<PlayFabError> onFailed);

        void GrantItems(string profileID, string[] itemsIDs, bool containPack, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);
    }
}
