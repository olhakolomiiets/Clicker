namespace CBS.Models
{
    public class ItemUsesCountChange
    {
        public string ItemInventoryID;
        public int UsesLeft;
        public bool Removed;
        public CBSInventoryItem ChangedItem;
    }
}
