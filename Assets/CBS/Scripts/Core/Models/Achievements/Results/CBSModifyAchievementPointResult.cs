

namespace CBS.Models
{
    public class CBSModifyAchievementPointResult : CBSBaseResult
    {
        public CBSTask Achievement;
        public bool CompleteAchievement;
        public bool CompleteTier;
        public GrantRewardResult ReceivedReward;
    }
}
