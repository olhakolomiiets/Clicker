

namespace CBS.Models
{
    public class FunctionModifyTaskResult<T> where T : CBSTask
    {
        public T Task;
        public bool CompleteTask;
        public bool CompleteTier;
        public GrantRewardResult RewardResult;
    }
}
