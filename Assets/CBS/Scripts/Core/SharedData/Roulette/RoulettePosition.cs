using System;

namespace CBS.Models
{
    [Serializable]
    public class RoulettePosition
    {
        public string ID;
        public string DisplayName;
        public int Weight;
        public RewardObject Reward;
        public ProfileEventContainer Events;
    }
}

