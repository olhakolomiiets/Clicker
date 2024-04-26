using CBS.Core;

namespace CBS.UI
{
    public class AchievementScroller : PreloadScroller<CBSTask>
    {
        protected override float DeltaToPreload => 0.7f;
    }
}
