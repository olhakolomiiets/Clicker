

using System;

namespace CBS.Models
{
    [Serializable]
    public class CalendarPosition
    {
        public int Position;
        public bool Active;
        public bool Rewarded;
        public bool Missed;
        public bool CanBeRewarded;
        public RewardObject Reward;
        public ProfileEventContainer Events;
    }
}
