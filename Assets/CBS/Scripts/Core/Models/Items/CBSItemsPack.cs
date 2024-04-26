using CBS.Models;
using PlayFab.ClientModels;
using System.Collections.Generic;

namespace CBS
{
    public class CBSItemPack : CBSBaseItem
    {
        public List<string> PackItemsIDs { get; private set; } = new List<string>();
        public Dictionary<string, uint> PackCurrecnies { get; private set; } = new Dictionary<string, uint>();

        public CBSItemPack(CatalogItem item)
        {
            bool tagExist = item.Tags != null && item.Tags.Count != 0;

            ItemID = item.ItemId;
            DisplayName = item.DisplayName;
            Description = item.Description;
            Category = tagExist ? item.Tags[0] : CBSConstants.UndefinedCategory;
            ExternalIconURL = item.ItemImageUrl;
            Prices = item.VirtualCurrencyPrices;
            ItemClass = item.ItemClass;
            CustomData = item.CustomData;

            PackItemsIDs = item.Bundle.BundledItems;
            PackCurrecnies = item.Bundle.BundledVirtualCurrencies;

            var baseData = GetCustomData<CBSItemCustomData>();
            Type = baseData == null ? ItemType.PACKS : baseData.ItemType;
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
