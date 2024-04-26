namespace CBS.Models
{
    public class CBSGetNextUpgradeStateResult : CBSBaseResult
    {
        public string ProfileID;
        public string ItemID;
        public string ItemInstanceID;
        public bool IsMax;
        public int NextUpgradeIndex;
        public int CurrentUpgradeIndex;
        public CBSItemUpgradeState NextUpgradeState;
        public CraftStateContainer DependencyState;
    }
}
