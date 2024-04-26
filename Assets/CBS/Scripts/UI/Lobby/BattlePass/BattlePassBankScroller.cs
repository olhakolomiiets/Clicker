using CBS.Core;
using CBS.Models;

namespace CBS.UI
{
    public class BattlePassBankScroller : PreloadScroller<BattlePassBankLevel>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
