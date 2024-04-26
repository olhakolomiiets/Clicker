using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSPurchaseItemWithRealMoneyResult : CBSBaseResult
    {
        public string ProfileID;
        public string ItemID;
        public string TransactionID;
        public List<CBSInventoryItem> PurchasedInstances;
        public Dictionary<string, uint> PurchasedCurrencies;
    }
}
