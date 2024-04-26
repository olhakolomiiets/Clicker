
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSOpenLootboxResult : CBSBaseResult
    {
        public List<CBSInventoryItem> GrantedItems;
        public Dictionary<string, uint> Currencies;
    }
}
