using CBS.Core;

namespace CBS.UI
{
    public class MessageDialogScroller : PreloadScroller<DialogSlotRequest>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
