using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class ClanLeaderboardScroller : PreloadScroller<ClanLeaderboardEntry>
    {
        protected override float DeltaToPreload => 0.75f;
    }
}
