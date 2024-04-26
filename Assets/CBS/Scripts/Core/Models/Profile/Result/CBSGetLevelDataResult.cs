using System;

namespace CBS.Models
{
    [Serializable]
    public class CBSLevelDataResult : CBSBaseResult
    {
        public LevelInfo LevelInfo;
        public bool IsNewLevel;
        public GrantRewardResult NewLevelReward;
    }
}
