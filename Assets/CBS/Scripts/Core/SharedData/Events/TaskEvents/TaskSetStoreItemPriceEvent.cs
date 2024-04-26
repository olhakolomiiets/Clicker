
namespace CBS.Models
{
    public class TaskSetStoreItemPriceEvent : TaskEvent
    {
        public string StoreID;
        public string ItemID;
        public string CurrencyCode;
        public int CurrencyValue;
    }
}
