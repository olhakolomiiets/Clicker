using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSPurchaseItemWithCurrencyResult : CBSBaseResult
    {
        public string ItemID;
        public List<CBSInventoryItem> PurchasedInstances;
        public Dictionary<string, uint> PurchasedCurrencies;
        public string PriceCode;
        public int PriceValue;
    }
}
