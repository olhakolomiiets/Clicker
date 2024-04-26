
using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class CBSClanTask : CBSTask
    {
        public int Weight = 1;

        public RewardObject ProfileReward;
        public ClanEventContainer ClanEvents;

        public ClanEventContainer GetClanEvents()
        {
            if (Type == TaskType.TIERED)
            {
                return CurrentTier?.ClanEvents;
            }
            else
            {
                return ClanEvents;
            }
        }

        public RewardObject GetNotRewardedObjectForProfile()
        {
            if (Type == TaskType.TIERED)
            {
                var taskState = TaskState;
                var tierList = TierList ?? new List<TaskTier>();
                var tierIndex = TierIndex;
                var completedTierList = IsComplete ? tierList : tierList.Take(tierIndex);
                var grantedRewards = taskState.GrantedRewards ?? new List<int>();
                var reward = new RewardObject();
                foreach (var tierTask in completedTierList)
                {
                    var taskIndex = tierTask.Index;
                    if (!grantedRewards.Contains(taskIndex))
                    {
                        var tierReward = tierTask.AdditionalReward;
                        reward = reward.MergeReward(tierReward);
                    }
                }
                return reward;
            }
            else
            {
                return Rewarded ? null : ProfileReward;
            }
        }
    }
}
