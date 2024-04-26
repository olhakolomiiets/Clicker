using CBS.Models;
using PlayFab.ClientModels;
using System.Collections.Generic;
using CBS.SharedData.Lootbox;

namespace CBS
{
    public class CBSLootbox : CBSBaseItem
    {
        public List<string> RandomItemsIDs { get; private set; }
        public Dictionary<string, uint> PackCurrecnies { get; private set; }
        public CBSLootboxTable LootboxTable { get; private set; }

        public CBSLootbox(CatalogItem item, CBSLootboxTable lootboxTable)
        {
            bool tagExist = item.Tags != null && item.Tags.Count != 0;
            LootboxTable = lootboxTable;

            ItemID = item.ItemId;
            DisplayName = item.DisplayName;
            Description = item.Description;
            Category = tagExist ? item.Tags[0] : CBSConstants.UndefinedCategory;
            ExternalIconURL = item.ItemImageUrl;
            Prices = item.VirtualCurrencyPrices;
            ItemClass = item.ItemClass;
            CustomData = item.CustomData;

            RandomItemsIDs = item.Container.ResultTableContents;
            PackCurrecnies = item.Container.VirtualCurrencyContents;

            var baseData = GetCustomData<CBSItemCustomData>();
            Type = baseData?.ItemType ?? ItemType.LOOT_BOXES;
        }

        public override CBSItemRecipe GetRecipeData()
        {
            return null;
        }

        public override List<CBSItemUpgradeState> GetUpgradeList()
        {
            return null;
        }
    }
}
