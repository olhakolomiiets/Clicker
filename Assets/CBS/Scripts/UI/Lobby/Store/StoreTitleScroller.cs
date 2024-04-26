using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class StoreTitleScroller : PreloadScroller<CBSStoreTitle>
    {
        protected override float DeltaToPreload => 0.75f;
    }
}
