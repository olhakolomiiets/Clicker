using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSFetchAllResult : CBSBaseResult
    {
        public List<CBSItem> Items;
        public List<CBSItemPack> Packs;
        public List<CBSLootbox> Lootboxes;
        public CBSRecipeContainer Recipes;
        public CBSItemUpgradesContainer Upgrades;

        public string[] ItemsCategories;
        public string[] PacksCategories;
        public string[] LootboxCategories;
    }
}
