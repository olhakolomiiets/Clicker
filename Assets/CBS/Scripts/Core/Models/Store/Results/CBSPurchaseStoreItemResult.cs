using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSPurchaseStoreItemResult : CBSBaseResult
    {
        public string ItemID;
        public List<CBSInventoryItem> PurchasedInstances;
        public Dictionary<string, uint> PurchasedCurrencies;
        public string PriceCode;
        public int PriceValue;
        public string StoreID;
        public StoreLimitationInfo LimitationInfo;
    }
}
