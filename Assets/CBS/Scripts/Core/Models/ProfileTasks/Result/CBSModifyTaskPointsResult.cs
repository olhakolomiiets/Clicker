
namespace CBS.Models
{
    public class CBSModifyTaskPointsResult : CBSBaseResult
    {
        public CBSTask Task;
        public bool CompleteTask;
        public bool CompleteTier;
        public GrantRewardResult ReceivedReward;
    }
}
