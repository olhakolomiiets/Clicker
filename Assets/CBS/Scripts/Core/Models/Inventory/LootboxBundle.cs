

using System.Collections.Generic;

namespace CBS.Models
{
    public class LootboxBundle
    {
        public List<CBSInventoryItem> GrantedItems;
        public Dictionary<string, uint> Currencies;
    }
}
