using CBS.Core;

namespace CBS.UI
{
    public class NotificationTitleScroller : PreloadScroller<NotificationSlotRequest>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
