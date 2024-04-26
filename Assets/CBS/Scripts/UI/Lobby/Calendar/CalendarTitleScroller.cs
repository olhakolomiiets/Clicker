using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class CalendarTitleScroller : PreloadScroller<CalendarInstance>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
