using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSAchievementsModule : CBSModule, IAchievements
    {
        /// <summary>
        /// Notify when player complete achievement.
        /// </summary>
        public event Action<CBSTask> OnCompleteAchievement;
        /// <summary>
        /// Notify when player complete achievement tier.
        /// </summary>
        public event Action<CBSTask> OnCompleteAchievementTier;
        /// <summary>
        /// Notify when player receive reward for achievement.
        /// </summary>
        public event Action<GrantRewardResult> OnProfileRewarded;

        private IProfile Profile { get; set; }
        private IFabAchievements FabAchievements { get; set; }

        protected override void Init()
        {
            Profile = Get<CBSProfileModule>();
            FabAchievements = FabExecuter.Get<FabAchievements>();
        }

        // API Methods

        /// <summary>
        /// Get information for all achievements and their player state.
        /// </summary>
        /// <param name="result"></param>
        public void GetAchievementsTable(Action<CBSGetAchievementsTableResult> result)
        {
            InternalGetAchievements(TasksState.ALL, result);
        }

        /// <summary>
        /// Get information for all available achievements for player achievements
        /// </summary>
        /// <param name="result"></param>
        public void GetActiveAchievementsTable(Action<CBSGetAchievementsTableResult> result)
        {
            InternalGetAchievements(TasksState.ACTIVE, result);
        }

        /// <summary>
        /// Get information for all completed achievements for player achievements
        /// </summary>
        /// <param name="result"></param>
        public void GetCompletedAchievementsTable(Action<CBSGetAchievementsTableResult> result)
        {
            InternalGetAchievements(TasksState.COMPLETED, result);
        }

        /// <summary>
        /// Adds a point to an achievement. For Achievements "OneShot" completes it immediately, for Achievements "Steps" - adds one step
        /// </summary>
        /// <param name="achievementID"></param>
        /// <param name="result"></param>
        public void AddAchievementPoint(string achievementID, Action<CBSModifyAchievementPointResult> result)
        {
            InternalModifyPoints(achievementID, 1, ModifyMethod.ADD, result);
        }

        /// <summary>
        /// Adds the points you specified to the achievement. More suitable for Steps achievements.
        /// </summary>
        /// <param name="achievementID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        public void AddAchievementPoint(string achievementID, int points, Action<CBSModifyAchievementPointResult> result)
        {
            InternalModifyPoints(achievementID, points, ModifyMethod.ADD, result);
        }

        /// <summary>
        /// Updates the achievement points you specified. More suitable for Steps achievements.
        /// </summary>
        /// <param name="achievementID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        public void UpdateAchievementPoint(string achievementID, int points, Action<CBSModifyAchievementPointResult> result)
        {
            InternalModifyPoints(achievementID, points, ModifyMethod.UPDATE, result);
        }

        /// <summary>
        /// Pick up a reward from a completed achievement if it hasn't been picked up before.
        /// </summary>
        /// <param name="achievementID"></param>
        /// <param name="result"></param>
        public void PickupAchievementReward(string achievementID, Action<CBSPickupAchievementRewardResult> result)
        {
            var profileID = Profile.ProfileID;

            FabAchievements.PickupReward(profileID, achievementID, onPick =>
            {
                var cbsError = onPick.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSPickupAchievementRewardResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onPick.GetResult<FunctionModifyTaskResult<CBSTask>>();
                    var achievement = functionResult.Task;
                    var reward = functionResult.RewardResult;

                    if (functionResult != null && reward != null)
                    {
                        var currencies = reward.GrantedCurrencies;
                        if (currencies != null)
                        {
                            var codes = currencies.Select(x => x.Key).ToArray();
                            Get<CBSCurrencyModule>().ChangeRequest(codes);
                        }

                        var grantedInstances = reward.GrantedInstances;
                        if (grantedInstances != null && grantedInstances.Count > 0)
                        {
                            var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                            Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        }

                        OnProfileRewarded?.Invoke(reward);
                    }

                    result?.Invoke(new CBSPickupAchievementRewardResult
                    {
                        IsSuccess = true,
                        Achievement = achievement,
                        ReceivedReward = reward
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSPickupAchievementRewardResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Reset the state of profile for a specific achievement.
        /// </summary>
        /// <param name="achievementID"></param>
        /// <param name="result"></param>
        public void ResetAchievement(string achievementID, Action<CBSResetAchievementResult> result)
        {
            var profileID = Profile.ProfileID;

            FabAchievements.ResetAchievement(profileID, achievementID, onReset =>
            {
                var cbsError = onReset.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSResetAchievementResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onReset.GetResult<FunctionModifyTaskResult<CBSTask>>();
                    var achievement = functionResult.Task;

                    result?.Invoke(new CBSResetAchievementResult
                    {
                        IsSuccess = true,
                        Achievement = achievement
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSResetAchievementResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get the number of completed achievements that did not receive rewards
        /// </summary>
        /// <param name="result"></param>
        public void GetAchievementsBadge(Action<CBSBadgeResult> result)
        {
            var profileID = Profile.ProfileID;

            FabAchievements.GetAchievementsBadge(profileID, onReset =>
            {
                var cbsError = onReset.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onReset.GetResult<FunctionBadgeResult>();
                    var badgeCount = functionResult.Count;

                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = true,
                        Count = badgeCount
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBadgeResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        // internal

        private void InternalModifyPoints(string achievementID, int points, ModifyMethod modify, Action<CBSModifyAchievementPointResult> result)
        {
            var profileID = Profile.ProfileID;

            FabAchievements.ModifyAchievementPoint(profileID, achievementID, points, modify, onAdd =>
            {
                var cbsError = onAdd.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyAchievementPointResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onAdd.GetResult<FunctionModifyTaskResult<CBSTask>>();
                    var achievement = functionResult.Task;
                    var reward = functionResult.RewardResult;
                    var complete = functionResult.CompleteTask;
                    var completeTier = functionResult.CompleteTier;

                    if (functionResult != null && reward != null)
                    {
                        var currencies = reward.GrantedCurrencies;
                        if (currencies != null)
                        {
                            var codes = currencies.Select(x => x.Key).ToArray();
                            Get<CBSCurrencyModule>().ChangeRequest(codes);
                        }
                        OnProfileRewarded?.Invoke(reward);

                        var grantedInstances = reward.GrantedInstances;
                        if (grantedInstances != null && grantedInstances.Count > 0)
                        {
                            var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                            Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        }
                    }

                    if (complete)
                    {
                        OnCompleteAchievement?.Invoke(achievement);
                    }
                    if (completeTier)
                    {
                        OnCompleteAchievementTier?.Invoke(achievement);
                    }

                    result?.Invoke(new CBSModifyAchievementPointResult
                    {
                        IsSuccess = true,
                        Achievement = achievement,
                        ReceivedReward = reward,
                        CompleteAchievement = complete,
                        CompleteTier = completeTier
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyAchievementPointResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        private void InternalGetAchievements(TasksState queryType, Action<CBSGetAchievementsTableResult> result)
        {
            var profileID = Profile.ProfileID;

            FabAchievements.GetProfileAchievements(profileID, queryType, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetAchievementsTableResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionTasksResult<CBSTask>>();
                    var achievementsList = functionResult.Tasks ?? new List<CBSTask>();

                    result?.Invoke(new CBSGetAchievementsTableResult
                    {
                        IsSuccess = true,
                        AchievementsData = new AchievementsData
                        {
                            Tasks = achievementsList
                        }
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetAchievementsTableResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }
    }
}
