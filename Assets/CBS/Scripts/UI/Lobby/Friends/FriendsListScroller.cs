using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class FriendsListScroller : PreloadScroller<ProfileEntity>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
