namespace CBS.Models
{
    public class FunctionGetNextUpgradeStateResult
    {
        public string ProfileID;
        public string ItemID;
        public string InventoryItemID;
        public bool IsMax;
        public int NextUpgradeIndex;
        public int CurrentUpgradeIndex;
        public CBSItemUpgradeState NextUpgradeState;
        public CraftStateContainer DependencyState;
    }
}
