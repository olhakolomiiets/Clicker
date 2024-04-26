using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class ClanTasksScroller : PreloadScroller<CBSClanTask>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
