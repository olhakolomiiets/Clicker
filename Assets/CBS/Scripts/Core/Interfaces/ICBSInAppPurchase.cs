using CBS.Models;
using System;

namespace CBS
{
    public interface ICBSInAppPurchase
    {
        event Action<IAPInitializeResult> OnInitialize;
        bool IsInitialized { get; }
        bool IsEnabled { get; }
        void PurchaseItem(string itemID, string catalogID, Action<IAPPurchaseItemResult> result);
        void PurchaseItem(string itemID, string catalogID, string storeID, Action<IAPPurchaseItemResult> result);
    }
}
