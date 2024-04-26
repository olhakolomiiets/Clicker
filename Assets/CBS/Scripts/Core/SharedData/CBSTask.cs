using CBS.Core;
using CBS.Models;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    [System.Serializable]
    public class CBSTask : ICustomData<CBSTaskCustomData>
    {
        // static data
        public string ID;
        public string Title;
        public string Description;
        public string Tag;
        public string PoolID;
        public TaskType Type;
        public int Steps;
        public bool IsLockedByLevel;
        public int LockLevel;
        public IntFilter LevelFilter;
        public string ExternalUrl;
        public RewardObject Reward;
        public List<TaskTier> TierList;

        public BaseTaskState TaskState;
        public ProfileEventContainer Events;

        // dynamic data
        public bool IsComplete => TaskState == null ? false : TaskState.IsComplete;
        public bool IsAvailable => TaskState == null ? true : TaskState.IsAvailable;
        public bool Rewarded => TaskState == null ? true : TaskState.Rewarded;
        public int TierIndex => TaskState == null ? 0 : TaskState.TierIndex;
        public int CurrentStep => TaskState == null ? 0 : TaskState.CurrentStep;
        public TaskTier CurrentTier
        {
            get
            {
                if (TierList == null || TierList.Count == 0)
                    return null;
                if (TierIndex >= TierList.Count)
                    return null;
                return TierList[TierIndex];
            }
        }


        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }
        public bool CompressCustomData => false;

        public void UpdateState(BaseTaskState state) => TaskState = state;

        public void AddNewTier(TaskTier tier)
        {
            TierList = TierList ?? new List<TaskTier>();
            TierList.Add(tier);
        }

        public void RemoveLastTier()
        {
            if (TierList == null || TierList.Count == 0)
                return;
            TierList.RemoveAt(TierList.Count - 1);
        }

        public int GetNextTierIndex()
        {
            var counts = TierList == null ? 0 : TierList.Count;
            return counts;
        }

        public T GetCustomData<T>() where T : CBSTaskCustomData
        {
            if (CompressCustomData)
                return JsonPlugin.FromJsonDecompress<T>(CustomRawData);
            else
                return JsonPlugin.FromJson<T>(CustomRawData);
        }

        public ProfileEventContainer GetEvents()
        {
            if (Type == TaskType.TIERED)
            {
                return CurrentTier?.Events;
            }
            else
            {
                return Events;
            }
        }

        public bool IsLevelLocking(int level)
        {
            if (!IsLockedByLevel)
            {
                return false;
            }
            if (LevelFilter == IntFilter.EQUAL)
            {
                return LockLevel != level;
            }
            else if (LevelFilter == IntFilter.EQUAL_OR_GREATER)
            {
                return (level < LockLevel);
            }
            else if (LevelFilter == IntFilter.EQUAL_OR_LESS)
            {
                return (level > LockLevel);
            }
            return false;
        }

        public bool IsRewardAvailable()
        {
            if (Type == TaskType.TIERED)
            {
                var reward = GetNotRewardedObject();
                return reward != null && !reward.IsEmpty() && !Rewarded;
            }
            return Reward != null && !Reward.IsEmpty() && !Rewarded;
        }

        public RewardObject GetNotRewardedObject()
        {
            if (Type == TaskType.TIERED)
            {
                var taskState = TaskState;
                var tierList = TierList ?? new List<TaskTier>();
                var tierIndex = TierIndex;
                var completedTierList = IsComplete ? tierList : tierList.Take(tierIndex);
                var grantedRewards = taskState.GrantedRewards ?? new List<int>();
                var reward = new RewardObject();
                foreach (var tierTask in completedTierList)
                {
                    var taskIndex = tierTask.Index;
                    if (!grantedRewards.Contains(taskIndex))
                    {
                        var tierReward = tierTask.Reward;
                        reward = reward.MergeReward(tierReward);
                    }
                }
                return reward;
            }
            else
            {
                return Rewarded ? null : Reward;
            }
        }

        public void MarkAsNotRewarded()
        {
            if (TaskState != null)
            {
                TaskState.Rewarded = false;
                TaskState.GrantedRewards?.Clear();
            }
        }

        public T Copy<T>() where T : CBSTask
        {
            return JsonPlugin.FromJson<T>(JsonPlugin.ToJson(this));
        }
    }
}
