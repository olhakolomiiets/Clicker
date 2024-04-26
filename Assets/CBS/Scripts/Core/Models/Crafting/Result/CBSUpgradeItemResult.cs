using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSUpgradeItemResult : CBSBaseResult
    {
        public string ProfileID;
        public int UpgradedLevelIndex;
        public CBSInventoryItem UpgradedItem;
        public List<string> SpendedInstanesIDs;
        public Dictionary<string, uint> SpendedCurrencies;
        public Dictionary<string, uint> ConsumedItems;
    }
}
