

using System.Collections.Generic;

namespace CBS.Models
{
    public class IAPPurchaseItemResult : CBSBaseResult
    {
        public string ProfileID;
        public string ItemID;
        public string TransactionID;
        public List<CBSInventoryItem> GrantedItems;
        public Dictionary<string, uint> GrantedCurrencies;
        public StoreLimitationInfo LimitationInfo;
    }
}
