namespace CBS.Models
{
    public class GrantItemsToClanRequest : FunctionBaseRequest
    {
        public string ClanID;
        public string[] ItemsIDs;
        public bool ContainPack;
    }
}
