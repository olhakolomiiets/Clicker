using System.Collections.Generic;

namespace CBS.Models
{
    public class CalendarProfileMeta
    {
        public Dictionary<int, bool> RewardedItems;

        public bool IsRewarded(int positionIndex)
        {
            if (RewardedItems == null)
                return false;
            return RewardedItems.ContainsKey(positionIndex);
        }

        public void AddReward(int index)
        {
            RewardedItems = RewardedItems ?? new Dictionary<int, bool>();
            RewardedItems[index] = true;
        }
    }
}

