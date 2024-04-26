

namespace CBS.Models
{
    public class FunctionValidateStorePurchaseRequest : FunctionBaseRequest
    {
        public string StoreID;
        public string ItemID;
        public bool IsPack;
        public int TimeZoneOffset;
    }
}
