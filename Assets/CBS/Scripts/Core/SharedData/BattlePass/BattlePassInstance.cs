using CBS.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class BattlePassInstance : ICustomData<CBSBattlePassCustomData>
    {
        private static readonly string BattlePassPrefix = "battle_pass_";
        private static readonly string FreeTicketID = "free";

        public string ID;
        public string InstanceID;
        public bool IsActive;
        public string DisplayName;
        public string Description;

        public bool ExtraLevelsEnabled;
        public bool TasksEnabled;
        public string TasksPoolID;
        public bool BankEnabled;
        public RewardDelivery BankRewardDelivery;
        public bool AutomaticRewardOnEnd;
        public RewardDelivery AutomaticRewardDelivery;
        public bool TimeLimitedRewardEnabled;
        public int AvailableRewardsPerDay;
        public float GainExpMultiplier;

        public int ExpStep = 100;
        public int DurationInHours;
        public DateTime? StartDate;
        public DateTime? EndDate;
        public List<BattlePassLevel> LevelTree;
        public List<BattlePassLevel> ExtraLevelTree;

        public BattlePassTicket FreeTicket;
        public List<BattlePassTicket> PaidTickets;

        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }

        public bool CompressCustomData => true;

        public virtual T GetCustomData<T>() where T : CBSBattlePassCustomData
        {
            if (CompressCustomData)
                return JsonPlugin.FromJsonDecompress<T>(CustomRawData);
            else
                return JsonPlugin.FromJson<T>(CustomRawData);
        }

        public string GetEventID()
        {
            return BattlePassPrefix + ID;
        }

        public BattlePassTicket GetFreeTicket()
        {
            if (FreeTicket == null)
                FreeTicket = new BattlePassTicket
                {
                    ID = FreeTicketID,
                    BattlePassID = ID,
                    ExpMultiply = 1f
                };
            return FreeTicket;
        }

        public List<BattlePassTicket> GetPaidTickets()
        {
            if (PaidTickets == null)
                PaidTickets = new List<BattlePassTicket>();
            return PaidTickets;
        }

        public void AddTicket(BattlePassTicket ticket)
        {
            PaidTickets = PaidTickets ?? new List<BattlePassTicket>();
            PaidTickets.Add(ticket);
        }

        public void RemoveTicket(BattlePassTicket ticket)
        {
            if (PaidTickets == null)
                return;
            if (PaidTickets.Contains(ticket))
            {
                PaidTickets.Remove(ticket);
            }
            PaidTickets.TrimExcess();
        }

        public int GetMaxExpValue()
        {
            var levelTree = LevelTree ?? new List<BattlePassLevel>();
            if (ExtraLevelsEnabled)
            {
                var extraLevelTree = ExtraLevelTree ?? new List<BattlePassLevel>();
                return (extraLevelTree.Count + levelTree.Count) * ExpStep;
            }
            else
            {
                return levelTree.Count * ExpStep;
            }
        }

        public int GetMaxLevelCount()
        {
            var levelTree = LevelTree ?? new List<BattlePassLevel>();
            if (ExtraLevelsEnabled)
            {
                var extraLevelTree = ExtraLevelTree ?? new List<BattlePassLevel>();
                return (extraLevelTree.Count + levelTree.Count);
            }
            else
            {
                return levelTree.Count;
            }
        }

        public List<BattlePassLevel> GetLevelTree()
        {
            var levelTree = LevelTree ?? new List<BattlePassLevel>();
            if (ExtraLevelsEnabled)
            {
                var extraLevelTree = ExtraLevelTree ?? new List<BattlePassLevel>();
                return levelTree.Concat(extraLevelTree).ToList();
            }
            else
            {
                return levelTree;
            }
        }

        public List<BattlePassBankLevel> GetBankLevels(int profilePassLevel, bool available)
        {
            var levelTree = GetLevelTree();
            var levelsCount = levelTree.Count;
            var bankLevels = new List<BattlePassBankLevel>();
            for (int i = 0; i < levelsCount; i++)
            {
                var level = levelTree[i];
                if (level.BankReward != null && !level.BankReward.IsEmpty())
                {
                    bankLevels.Add(new BattlePassBankLevel
                    {
                        TargetIndex = i,
                        Reward = level.BankReward,
                        Reached = profilePassLevel >= i,
                        Available = available
                    });
                }
            }
            return bankLevels;
        }
    }
}
