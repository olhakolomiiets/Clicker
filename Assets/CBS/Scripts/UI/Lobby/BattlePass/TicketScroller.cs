using CBS.Core;

namespace CBS.UI
{
    public class TicketScroller : PreloadScroller<TicketUIRequest>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
