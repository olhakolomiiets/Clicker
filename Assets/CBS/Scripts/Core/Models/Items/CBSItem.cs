using CBS.Models;
using PlayFab.ClientModels;
using System.Collections.Generic;

namespace CBS
{
    public class CBSItem : CBSBaseItem
    {
        public bool IsConsumable { get; private set; }
        public bool IsStackable { get; private set; }
        public bool IsTradable { get; private set; }
        public bool HasLifeTime { get; private set; }
        public int UsageCount { get; private set; }
        public int LifeTime { get; private set; }
        public bool IsEquippable { get; private set; }
        public bool IsRecipe { get; private set; }
        public CBSItemRecipe RecipeData { get; private set; }
        public bool IsUpgradable { get; private set; }

        private List<CBSItemUpgradeState> UpgradeData { get; set; }

        public CBSItem(CatalogItem item, CBSItemRecipe recipeData, List<CBSItemUpgradeState> upgradeInfo)
        {
            bool tagExist = item.Tags != null && item.Tags.Count != 0;

            ItemID = item.ItemId;
            DisplayName = item.DisplayName;
            Description = item.Description;
            Category = tagExist ? item.Tags[0] : CBSConstants.UndefinedCategory;
            IsConsumable = item.Consumable != null && item.Consumable.UsageCount > 0;
            UsageCount = item.Consumable == null || item.Consumable.UsageCount == null ? 0 : (int)item.Consumable.UsageCount;
            IsStackable = item.IsStackable;
            IsTradable = item.IsTradable;
            HasLifeTime = item.Consumable != null && item.Consumable.UsagePeriod != null;
            LifeTime = item.Consumable == null || item.Consumable.UsagePeriod == null ? 0 : (int)item.Consumable.UsagePeriod;
            ExternalIconURL = item.ItemImageUrl;
            Prices = item.VirtualCurrencyPrices;
            ItemClass = item.ItemClass;
            CustomData = item.CustomData;

            var baseData = GetCustomData<CBSItemCustomData>();
            IsEquippable = baseData == null ? false : baseData.IsEquippable;
            IsRecipe = baseData == null ? false : baseData.IsRecipe;
            Type = baseData == null ? ItemType.ITEMS : baseData.ItemType;

            RecipeData = recipeData;
            UpgradeData = upgradeInfo;
            IsUpgradable = UpgradeData != null && UpgradeData.Count > 0;
        }

        public override CBSItemRecipe GetRecipeData()
        {
            return RecipeData;
        }

        public override List<CBSItemUpgradeState> GetUpgradeList()
        {
            return UpgradeData;
        }
    }
}
