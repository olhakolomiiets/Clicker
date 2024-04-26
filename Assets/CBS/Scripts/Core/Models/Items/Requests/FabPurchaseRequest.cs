namespace CBS.Models
{
    public class FabPurchaseRequest
    {
        public string ProfileID;
        public string ItemID;
        public string StoreID;
        public string CurrencyCode;
        public int CurrencyValue;
        public ItemType ItemType;
        public bool CheckLimitation;
    }
}
