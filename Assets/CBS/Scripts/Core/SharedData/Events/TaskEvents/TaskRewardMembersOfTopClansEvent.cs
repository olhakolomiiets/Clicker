
namespace CBS.Models
{
    public class TaskRewardMembersOfTopClansEvent : TaskEvent
    {
        public RewardObject Reward;
        public int nTop;
        public string StatisticName;
    }
}
