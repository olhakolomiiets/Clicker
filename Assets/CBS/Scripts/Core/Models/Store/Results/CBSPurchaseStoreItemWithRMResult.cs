using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSPurchaseStoreItemWithRMResult : CBSBaseResult
    {
        public string ProfileID;
        public string ItemID;
        public string StoreID;
        public string TransactionID;
        public List<CBSInventoryItem> PurchasedInstances;
        public Dictionary<string, uint> PurchasedCurrencies;
        public StoreLimitationInfo LimitationInfo;
    }
}
