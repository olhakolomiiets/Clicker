using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSCraftResult : CBSBaseResult
    {
        public CBSInventoryItem CraftedItemInstance;
        public List<string> SpendedInstancesIDs;
        public Dictionary<string, uint> SpendedCurrencies;
        public Dictionary<string, uint> ConsumedItems;
    }
}
