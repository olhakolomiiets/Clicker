using System.Collections.Generic;
using CBS.Models;

namespace CBS
{
    public static class TaskUtils
    {
        public static BaseTaskState ApplyPlayerState(this CBSTask task, Dictionary<string, BaseTaskState> profileData, int level = 0)
        {
            var id = task.ID;
            var type = task.Type;
            var isLockedByLevel = task.IsLockedByLevel;
            var lockLevel = task.LockLevel;
            
            var newTaskState = new BaseTaskState();

            if (profileData.ContainsKey(id))
            {
                var taskObject = !profileData.ContainsKey(id) ? new BaseTaskState {
                    IsComplete = false,
                    CurrentStep = 0,
                    Rewarded = false,
                    TierIndex = 0,
                    GrantedRewards = new List<int>()
                } : profileData[id];
                
                // check complete
                newTaskState.IsComplete = taskObject.IsComplete;
                
                // check steps
                if (type == TaskType.STEPS || type == TaskType.TIERED)
                {
                    newTaskState.CurrentStep = taskObject.CurrentStep;
                }
                // check reward
                newTaskState.Rewarded = taskObject.Rewarded;
                newTaskState.TierIndex = taskObject.TierIndex;
                newTaskState.GrantedRewards = taskObject.GrantedRewards;
            }
            else
            {
                // set default state
                newTaskState.IsComplete = false;
                newTaskState.CurrentStep = 0;
                newTaskState.Rewarded = false;
                newTaskState.TierIndex = 0;
                newTaskState.GrantedRewards = new List<int>();
            }     
            
            // check lock
            if (isLockedByLevel)
            {
                newTaskState.IsAvailable = !task.IsLevelLocking(level);
            }
            else
            {
                newTaskState.IsAvailable = true;
            }
            task.UpdateState(newTaskState);

            // check tier
            if (type == TaskType.TIERED)
            {
                var tier = task.CurrentTier;
                var tierList = task.TierList ?? new List<TaskTier>();
                if (tier != null)
                {
                    task.Steps = tier.StepsToComplete;
                    if (tier.OverrideDescription)
                    {
                        task.Description = tier.Description;
                    }
                    tier.CurrentSteps = newTaskState.CurrentStep;
                }
                // check rewards
                var notRewardedObject = task.GetNotRewardedObject();
                newTaskState.Rewarded = notRewardedObject.IsEmpty();
                task.Reward = tier.Reward;
            }

            return newTaskState;
        }

        public static BaseTaskState AddPoints(this CBSTask task, int points, ModifyMethod method)
        {
            var state = task.TaskState;
            var type = task.Type;
            var curSteps = state.CurrentStep;
            var maxSteps = task.Steps;      

            if (type == TaskType.STEPS)
            {
                if (method == ModifyMethod.ADD)
                {
                    curSteps = curSteps + points;
                }
                else if (method == ModifyMethod.UPDATE)
                {
                    curSteps = points;
                }
                
                if (curSteps >= maxSteps)
                {
                    state.IsComplete = true;
                    curSteps = maxSteps;
                }
            }
            else if (type == TaskType.TIERED)
            {
                if (method == ModifyMethod.UPDATE)
                {
                    curSteps = points;
                }
                else if (method == ModifyMethod.ADD)
                {
                    var tierIndex = task.TierIndex;
                    var tierList = task.TierList ?? new List<TaskTier>();
                    var wasCompleteInIteration = false;
                    for (int i = tierIndex; i < tierList.Count; i++)
                    {
                        var tier = tierList[i];
                        var isComplete = tier.AddPoints(points, curSteps);
                        
                        if (isComplete)
                        {
                            points -= tier.StepsToComplete - curSteps;
                            var isLastLevel = i == tierList.Count - 1;
                            if (isLastLevel)
                            {
                                state.TierIndex = i;
                                state.IsComplete = true;                              
                                curSteps = tier.StepsToComplete;
                            }
                            else
                            {
                                state.TierIndex = i + 1;
                                curSteps = 0;
                                var nextTier = tierList[i + 1];
                                task.Steps = nextTier.StepsToComplete;
                                if (nextTier.OverrideDescription)
                                    task.Description = nextTier.Description;
                                task.Reward = nextTier.Reward;
                            }
                            state.Rewarded = false;
                            wasCompleteInIteration = true;
                        }
                        else
                        {
                            if (wasCompleteInIteration)
                            {
                                curSteps = points;
                                wasCompleteInIteration = false;
                            }
                            else
                            {
                                curSteps += points;
                            }
                            break;
                        }
                    }
                }
            }
            else
            {
                state.IsComplete = true;
            }
            state.CurrentStep = curSteps;
            task.UpdateState(state);
            return state;
        }
    }
}