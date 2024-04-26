namespace CBS.Models
{
    public class ItemDependencyState
    {
        public string ID;
        public int NeedCount;
        public int PresentCount;
        public ItemDependencyType Type;

        public bool IsValid()
        {
            return PresentCount >= NeedCount;
        }
    }
}
