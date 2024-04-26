using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class MatchmakingScroller : PreloadScroller<CBSMatchmakingQueue>
    {
        protected override float DeltaToPreload => 0.75f;
    }
}