using System;

namespace CBS.Models
{
    public class BattlePassUserInfo : BattlePassInstanceAccess
    {
        public string ProfileID;
        public string BattlePassID;
        public string InstanceID;
        public string BattlePassName;
        public bool IsActive;
        public int PlayerLevel;
        public int PlayerExp;
        public int ExpOfCurrentLevel;
        public int ExpStep;
        public int RewardBadgeCount;
        public int[] CollectedSimpleReward;
        public int[] CollectedPremiumReward;
        public string[] PurchasedTickets;
        public DateTime? EndDate;
        public DateTime? LimitStartDate;

        public string CustomDataClassName;
        public string CustomRawData;

        public virtual T GetCustomData<T>() where T : CBSBattlePassCustomData
        {
            return JsonPlugin.FromJsonDecompress<T>(CustomRawData);
        }
    }
}
