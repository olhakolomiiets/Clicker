using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetItemsResult : CBSBaseResult
    {
        public List<CBSItem> Items;
        public List<CBSItemPack> Packs;
        public List<CBSLootbox> Lootboxes;
    }
}
