using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetLootboxesResult : CBSBaseResult
    {
        public List<CBSLootboxInventoryItem> Lootboxes;
    }
}
