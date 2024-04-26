using CBS.Core;

namespace CBS.UI
{
    public class ClanMembersScroller : PreloadScroller<ClanMemberUIRequest>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
