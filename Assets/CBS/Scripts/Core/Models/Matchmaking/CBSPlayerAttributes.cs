using System;

namespace CBS.Models
{
    [Serializable]
    public class CBSPlayerAttributes
    {
        public string ProfileID;
        public string LevelEquality;
        public string MatchmakingStringEquality;
        public double? LevelDifference;
        public double? ValueDifference;
    }
}

