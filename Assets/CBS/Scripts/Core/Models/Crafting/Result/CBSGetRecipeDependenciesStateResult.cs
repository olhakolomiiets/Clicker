namespace CBS.Models
{
    public class CBSGetRecipeDependencyStateResult : CBSBaseResult
    {
        public string ItemIDToCraft;
        public CraftStateContainer DependencyState;
    }
}
