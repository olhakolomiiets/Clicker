using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class PlayerLeaderboardScroller : PreloadScroller<ProfileLeaderboardEntry>
    {
        protected override float DeltaToPreload => 0.75f;
    }
}
