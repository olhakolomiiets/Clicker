
namespace CBS.Models
{
    public class CBSConsumeInventoryItemResult : CBSBaseResult
    {
        public string ProfileID;
        public string InstanceId;
        public CBSInventoryItem ConsumedItem;
        public int CountLeft;
    }
}
