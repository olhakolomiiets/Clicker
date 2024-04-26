using System.Collections.Generic;

namespace CBS
{
    public class BaseTaskState
    {
        public bool IsComplete;
        public int CurrentStep;
        public bool Rewarded;
        public bool IsAvailable;
        public int TierIndex;
        public List<int> GrantedRewards;

        public void MarkRewardsAsGranted()
        {
            if (TierIndex == 0)
                return;
            GrantedRewards = new List<int>();
            if (IsComplete)
            {
                for (int i = 0; i <= TierIndex; i++)
                {
                    GrantedRewards.Add(i);
                }
            }
            else
            {
                for (int i = 1; i < TierIndex + 1; i++)
                {
                    GrantedRewards.Add(i - 1);
                }
            }
        }
    }
}