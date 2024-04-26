using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class StoreContentScroller : PreloadScroller<CBSStoreItem>
    {
        protected override float DeltaToPreload => 0.75f;
    }
}
