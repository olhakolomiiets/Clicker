using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class StickerScroller : PreloadScroller<ChatSticker>
    {
        protected override float DeltaToPreload => 0.7f;

        public void SetPoolCount(int count)
        {
            PoolCount = count;
        }
    }
}
