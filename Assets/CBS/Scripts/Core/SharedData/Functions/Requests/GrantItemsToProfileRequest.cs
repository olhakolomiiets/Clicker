namespace CBS.Models
{
    public class GrantItemsToProfileRequest : FunctionBaseRequest
    {
        public string[] ItemsIDs;
        public bool ContainPack;
    }
}
