namespace CBS.Models
{
    public class CBSGetItemResult : CBSBaseResult
    {
        public CBSItem Item;
        public CBSItemPack Pack;
        public CBSLootbox Lootbox;

        public ItemType ItemType
        {
            get
            {
                if (Item != null)
                    return ItemType.ITEMS;
                else if (Pack != null)
                    return ItemType.PACKS;
                else if (Lootbox != null)
                    return ItemType.LOOT_BOXES;
                return ItemType.ITEMS;
            }
        }
    }
}
