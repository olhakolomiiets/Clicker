using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class EventsScroller : PreloadScroller<CBSEvent>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
