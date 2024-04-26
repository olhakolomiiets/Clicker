using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class CBSGetInventoryResult : CBSBaseResult
    {
        public string TargetID { get; private set; }
        private List<CBSInventoryItem> CBSInstances;

        public CBSGetInventoryResult() { }

        public CBSGetInventoryResult(List<CBSInventoryItem> instances, string profileID)
        {
            CBSInstances = instances;
            TargetID = profileID;
        }

        public List<CBSInventoryItem> AllItems => CBSInstances.Where(x => x.Type == ItemType.ITEMS && !x.IsInTrading).ToList();
        public List<CBSInventoryItem> Lootboxes => CBSInstances.Where(x => x.Type == ItemType.LOOT_BOXES).ToList();
        public List<CBSInventoryItem> EquippedItems => AllItems.Where(x => x.Equipped).ToList();
        public List<CBSInventoryItem> NonEquippedItems => AllItems.Where(x => !x.Equipped).ToList();
        public List<CBSInventoryItem> EquippableItems => AllItems.Where(x => x.IsEquippable).ToList();
        public List<CBSInventoryItem> TradableItems => AllItems.Where(x => x.IsTradable).ToList();
        public List<CBSInventoryItem> ConsumableItems => AllItems.Where(x => x.IsConsumable).ToList();
        public List<CBSInventoryItem> RecipeItems => AllItems.Where(x => x.IsRecipe).ToList();
        public List<CBSInventoryItem> IsInTradingItems => CBSInstances.Where(x => x.IsInTrading).ToList();
    }
}
