
namespace CBS.Models
{
    public class CBSModifyClanTaskPointsResult : CBSBaseResult
    {
        public CBSClanTask Task;
        public bool CompleteTask;
        public bool CompleteTier;
        public GrantRewardResult ReceivedReward;
    }
}
