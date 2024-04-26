namespace CBS.Models
{
    public class FunctionIAPPostProcessRequest : FunctionBaseRequest
    {
        public string ItemID;
        public string CatalogID;
        public bool IsPack;
        public string StoreID;
        public int TimeZoneOffset;
    }
}
