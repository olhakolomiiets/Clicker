
namespace CBS.Models
{
    public class CBSModifyItemUsesCountResult : CBSBaseResult
    {
        public string ProfileID;
        public string InstanceId;
        public CBSInventoryItem UpdatedItem;
        public int UpdatedCount;
    }
}
