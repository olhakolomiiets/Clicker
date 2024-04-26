using CBS.Core;

namespace CBS.UI
{
    public class SimpleItemScroller : PreloadScroller<CBSInventoryItem>
    {
        protected override float DeltaToPreload => 0.7f;

        public void SetPoolCount(int count)
        {
            PoolCount = count;
        }
    }
}
