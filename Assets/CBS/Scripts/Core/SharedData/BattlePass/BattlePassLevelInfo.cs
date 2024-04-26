
using System;
using System.Linq;

namespace CBS.Models
{
    public class BattlePassLevelInfo
    {
        public string BattlePassID { get; private set; }
        public BattlePassLevel LevelDetail { get; private set; }
        public int LevelIndex { get; private set; }
        public int ProfilePassLevel { get; private set; }
        public int ExpOfCurrentLevel { get; private set; }
        public int ExpStep { get; private set; }
        public bool IsPassActive { get; private set; }
        public bool PremiumAccess { get; private set; }
        public bool ExtraLevelAccess { get; private set; }
        public bool IsExtraLevel { get; private set; }
        public bool IsPremiumRewardCollected { get; private set; }
        public bool IsDefaultRewardCollected { get; private set; }
        public bool IsLast { get; private set; }
        public int MaxIndex { get; private set; }

        private BattlePassInstance PassInstance { get; set; }
        private BattlePassUserInfo State { get; set; }


        public BattlePassLevelInfo(BattlePassInstance instance, BattlePassUserInfo profileState, BattlePassLevel level, int index, int maxIndex)
        {
            PassInstance = instance;
            State = profileState;
            LevelDetail = level;
            MaxIndex = maxIndex;

            var collectedDefaultReward = State.CollectedSimpleReward;
            var collectedPremiumReward = State.CollectedPremiumReward;

            LevelIndex = index;
            BattlePassID = State.BattlePassID;
            ExpStep = PassInstance.ExpStep;
            IsPassActive = State.IsActive;
            ExpOfCurrentLevel = State.ExpOfCurrentLevel;
            IsDefaultRewardCollected = collectedDefaultReward == null ? false : collectedDefaultReward.Contains(LevelIndex);
            IsPremiumRewardCollected = collectedPremiumReward == null ? false : collectedPremiumReward.Contains(LevelIndex);
            PremiumAccess = State.PremiumAccess;
            ExtraLevelAccess = State.ExtraLevelAccess;
            IsExtraLevel = LevelDetail.IsExtraLevel;
            ProfilePassLevel = State.PlayerLevel;
            IsLast = maxIndex == LevelIndex;
        }

        public void RebuildLevel(BattlePassLevel level, int index)
        {
            LevelDetail = level;
            LevelIndex = index;
            IsExtraLevel = LevelDetail.IsExtraLevel;
            var collectedDefaultReward = State.CollectedSimpleReward;
            var collectedPremiumReward = State.CollectedPremiumReward;
            IsDefaultRewardCollected = collectedDefaultReward == null ? false : collectedDefaultReward.Contains(LevelIndex);
            IsPremiumRewardCollected = collectedPremiumReward == null ? false : collectedPremiumReward.Contains(LevelIndex);
        }

        public void ForceEnablePass()
        {
            IsPassActive = true;
        }

        public void CollectReward(BattlePassRewardType type)
        {
            if (type == BattlePassRewardType.DEFAULT)
                IsDefaultRewardCollected = true;
            else if (type == BattlePassRewardType.PREMIUM)
                IsPremiumRewardCollected = true;
        }

        public bool RewardIsActive(BattlePassRewardType type)
        {
            if (type == BattlePassRewardType.DEFAULT)
            {
                return (ProfilePassLevel < LevelIndex && !IsRewardLockedByTimer() || IsRewardAvailableToCollect(type)) && IsPassActive && !IsExtraLevelLocked();
            }
            else if (type == BattlePassRewardType.PREMIUM)
            {
                return (ProfilePassLevel < LevelIndex && !IsRewardLockedByTimer() || IsRewardAvailableToCollect(type)) && PremiumAccess && IsPassActive && !IsExtraLevelLocked();
            }
            return false;
        }

        public bool IsRewardAvailableToCollect(BattlePassRewardType type)
        {
            if (type == BattlePassRewardType.DEFAULT)
            {
                return !IsRewardCollected(type) && LevelIndex <= ProfilePassLevel && IsPassActive && !IsRewardLocked(type);
            }
            else if (type == BattlePassRewardType.PREMIUM)
            {
                return !IsRewardCollected(type) && PremiumAccess && LevelIndex <= ProfilePassLevel && IsPassActive && !IsRewardLocked(type);
            }
            return false;
        }

        public bool IsRewardCollected(BattlePassRewardType type)
        {
            if (type == BattlePassRewardType.DEFAULT)
            {
                return IsDefaultRewardCollected;
            }
            else if (type == BattlePassRewardType.PREMIUM)
            {
                return IsPremiumRewardCollected;
            }
            return false;
        }

        public RewardObject GetReward(BattlePassRewardType type)
        {
            LevelDetail = LevelDetail ?? new BattlePassLevel();
            if (type == BattlePassRewardType.DEFAULT)
            {
                return LevelDetail.DefaultReward;
            }
            else if (type == BattlePassRewardType.PREMIUM)
            {
                return LevelDetail.PremiumReward;
            }
            return null;
        }

        public bool IsRewardLocked(BattlePassRewardType type)
        {
            if (!IsPassActive)
                return false;
            if (IsExtraLevel)
            {
                if (type == BattlePassRewardType.PREMIUM)
                {
                    return !(PremiumAccess && ExtraLevelAccess && !IsRewardLockedByTimer());
                }
                else
                {
                    return !(ExtraLevelAccess && !IsRewardLockedByTimer());
                }
            }
            else
            {
                if (type == BattlePassRewardType.PREMIUM)
                {
                    return !(PremiumAccess && !IsRewardLockedByTimer());
                }
                else
                {
                    return IsRewardLockedByTimer();
                }
            }
        }

        public bool IsRewardLockedByTimer(DateTime? requestDate = null)
        {
            if (requestDate == null)
                requestDate = DateTime.Now;
            var availablePerDay = PassInstance.AvailableRewardsPerDay;
            if (!PassInstance.TimeLimitedRewardEnabled || availablePerDay == 0)
                return false;
            if (State.DisableTimeLimit)
                return false;
            var startDate = State.LimitStartDate;
            if (startDate == null)
                return false;
            var limitIndex = (LevelIndex) / availablePerDay;
            var checkDate = startDate.GetValueOrDefault().AddDays(limitIndex);
            return checkDate.Subtract(requestDate.GetValueOrDefault()).Ticks > 0;
        }

        public DateTime? GetLimitDate(DateTime? requestDate = null)
        {
            if (requestDate == null)
                requestDate = DateTime.Now;
            var availablePerDay = PassInstance.AvailableRewardsPerDay;
            if (!PassInstance.TimeLimitedRewardEnabled || availablePerDay == 0)
                return null;
            if (State.DisableTimeLimit)
                return null;
            var startDate = State.LimitStartDate;
            if (startDate == null)
                return null;
            var limitIndex = (LevelIndex + 1) / availablePerDay;
            var checkDate = startDate.GetValueOrDefault().AddDays(limitIndex);
            return checkDate;
        }

        public bool IsExtraLevelLocked()
        {
            if (!IsExtraLevel)
                return false;
            return !ExtraLevelAccess;
        }

        public bool IsCurrent()
        {
            return ProfilePassLevel == LevelIndex;
        }

        public bool IsPassed()
        {
            return ProfilePassLevel > LevelIndex;
        }
    }
}
