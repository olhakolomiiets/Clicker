using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class AvatarListScroller : PreloadScroller<CBSAvatarState>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
