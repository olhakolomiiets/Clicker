
namespace CBS.Models
{
    public class CBSModifyProfileTaskPointsResult : CBSBaseResult
    {
        public CBSProfileTask Task;
        public bool CompleteTask;
        public bool CompleteTier;
        public GrantRewardResult ReceivedReward;
    }
}
