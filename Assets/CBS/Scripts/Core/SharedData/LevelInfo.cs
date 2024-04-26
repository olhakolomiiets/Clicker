using System;

namespace CBS.Models
{
    [Serializable]
    public class LevelInfo
    {
        public string TargetID;
        public CBSEntityType Type;
        public int CurrentLevel;
        public int PrevLevelExp;
        public int NextLevelExp;
        public int CurrentExp;
        public RewardObject NextLevelReward;

        public bool ReachMaxLevel
        {
            get
            {
                return CurrentExp >= NextLevelExp && CurrentExp != 0;
            }
        }

        public FunctionAddExpirienceResult ToAddExpResult(bool reachNewLevel, GrantRewardResult newLevelReward)
        {
            return new FunctionAddExpirienceResult
            {
                TargetID = this.TargetID,
                Type = this.Type,
                CurrentLevel = this.CurrentLevel,
                PrevLevelExp = this.PrevLevelExp,
                NextLevelExp = this.NextLevelExp,
                CurrentExp = this.CurrentExp,
                NextLevelReward = this.NextLevelReward,
                NewLevelReached = reachNewLevel,
                NewLevelReward = newLevelReward
            };
        }
    }
}
