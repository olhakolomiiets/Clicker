using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class ItemDependencyScroller : PreloadScroller<ItemDependencyState>
    {
        protected override float DeltaToPreload => 0.75f;
    }
}
