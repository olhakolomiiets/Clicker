using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSProfileTasksModule : CBSModule, IProfileTasks
    {
        /// <summary>
        /// Notify when player complete task.
        /// </summary>
        public event Action<CBSTask> OnCompleteTask;
        /// <summary>
        /// Notify when player complete task tier.
        /// </summary>
        public event Action<CBSTask> OnCompleteTaskTier;
        /// <summary>
        /// Notify when player reset tasks and get new
        /// </summary>
        public event Action<List<CBSTask>> OnTasksReseted;
        /// <summary>
        /// Notify when profile receive reward for task.
        /// </summary>
        public event Action<GrantRewardResult> OnRewardGranted;

        private IProfile Profile { get; set; }
        private IFabProfileTasks FabProfileTasks { get; set; }

        protected override void Init()
        {
            Profile = Get<CBSProfileModule>();
            FabProfileTasks = FabExecuter.Get<FabProfileTasks>();
        }

        // API methods

        /// <summary>
        /// Get list of all tasks from pool.
        /// </summary>
        /// <param name="result"></param>
        public void GetAllTasksFromPool(string tasksPoolID, Action<CBSGetTasksFromPoolResult> result)
        {
            var profileID = Profile.ProfileID;

            FabProfileTasks.GetTasksPool(profileID, tasksPoolID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetTasksFromPoolResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionTasksPoolResult>();
                    var tasks = functionResult.Tasks;

                    result?.Invoke(new CBSGetTasksFromPoolResult
                    {
                        IsSuccess = true,
                        Tasks = tasks
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetTasksFromPoolResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get tasks available for profile from pool
        /// </summary>
        /// <param name="result"></param>
        public void GetTasksForProfile(string tasksPoolID, Action<CBSGetTasksForProfileResult> result)
        {
            var profileID = Profile.ProfileID;

            FabProfileTasks.GetTasksForProfile(profileID, tasksPoolID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetTasksForProfileResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionProfileTasksResult>();
                    var tasks = functionResult.Tasks;
                    var resetDate = functionResult.NextResetDate;

                    result?.Invoke(new CBSGetTasksForProfileResult
                    {
                        IsSuccess = true,
                        Tasks = tasks,
                        ResetDate = resetDate
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetTasksForProfileResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Adds a points to multiplicity task. For Tasks "OneShot" completes it immediately, for Tasks "Steps" - adds one step
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        public void AddMultiplicityTasksPoint(string tasksPoolID, Dictionary<string, int> modifyPair, Action<CBSModifyProfileMultiplyTaskResult> result)
        {
            InternalModifyMultiplyPoints(tasksPoolID, ModifyMethod.ADD, modifyPair, result);
        }

        /// <summary>
        /// Updates points of multiplicity tasks you specified. More suitable for Steps tasks.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        public void UpdateMultiplicityTasksPoint(string tasksPoolID, Dictionary<string, int> modifyPair, Action<CBSModifyProfileMultiplyTaskResult> result)
        {
            InternalModifyMultiplyPoints(tasksPoolID, ModifyMethod.UPDATE, modifyPair, result);
        }

        /// <summary>
        /// Adds a point to an task. For Tasks "OneShot" completes it immediately, for Tasks "Steps" - adds one step
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        public void AddTaskPoint(string tasksPoolID, string taskID, Action<CBSModifyProfileTaskPointsResult> result)
        {
            InternalModifyPoints(tasksPoolID, taskID, 1, ModifyMethod.ADD, result);
        }

        /// <summary>
        /// Adds the points you specified to the task. More suitable for Steps task.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        public void AddTaskPoints(string tasksPoolID, string taskID, int points, Action<CBSModifyProfileTaskPointsResult> result)
        {
            InternalModifyPoints(tasksPoolID, taskID, points, ModifyMethod.ADD, result);
        }

        /// <summary>
        /// Adds the points you specified to the task. More suitable for Steps task.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        public void AddTaskPoints(CBSTask task, int points, Action<CBSModifyProfileTaskPointsResult> result)
        {
            var tasksPoolID = task.PoolID;
            var taskID = task.ID;
            InternalModifyPoints(tasksPoolID, taskID, points, ModifyMethod.ADD, result);
        }

        /// <summary>
        /// Updates the task points you specified. More suitable for Steps tasks.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        public void UpdateTaskPoint(string tasksPoolID, string taskID, int points, Action<CBSModifyProfileTaskPointsResult> result)
        {
            InternalModifyPoints(tasksPoolID, taskID, points, ModifyMethod.UPDATE, result);
        }

        /// <summary>
        /// Updates the task points you specified. More suitable for Steps tasks.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        public void UpdateTaskPoint(CBSTask task, int points, Action<CBSModifyProfileTaskPointsResult> result)
        {
            var tasksPoolID = task.PoolID;
            var taskID = task.ID;
            InternalModifyPoints(tasksPoolID, taskID, points, ModifyMethod.UPDATE, result);
        }

        /// <summary>
        /// Pick up a reward from a completed task if it hasn't been picked up before.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        public void PickupTaskReward(string tasksPoolID, string taskID, Action<CBSModifyProfileTaskPointsResult> result)
        {
            var profileID = Profile.ProfileID;

            FabProfileTasks.PickupReward(profileID, taskID, tasksPoolID, onPick =>
            {
                var cbsError = onPick.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyProfileTaskPointsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onPick.GetResult<FunctionModifyTaskResult<CBSProfileTask>>();
                    var tasks = functionResult.Task;
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

                        OnRewardGranted?.Invoke(reward);
                    }

                    result?.Invoke(new CBSModifyProfileTaskPointsResult
                    {
                        IsSuccess = true,
                        Task = tasks,
                        ReceivedReward = reward
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyProfileTaskPointsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Reset tasks states for pool and get new random tasks.
        /// </summary>
        /// <param name="result"></param>
        public void ResetAndGetNewTasks(string tasksPoolID, Action<CBSGetTasksForProfileResult> result)
        {
            var profileID = Profile.ProfileID;

            FabProfileTasks.RegenerateNewTasks(profileID, tasksPoolID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetTasksForProfileResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionProfileTasksResult>();
                    var tasks = functionResult.Tasks;
                    var resetDate = functionResult.NextResetDate;

                    result?.Invoke(new CBSGetTasksForProfileResult
                    {
                        IsSuccess = true,
                        Tasks = tasks,
                        ResetDate = resetDate
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetTasksForProfileResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get the number of completed tasks that did not receive rewards
        /// </summary>
        /// <param name="result"></param>
        public void GetTasksBadge(string tasksPoolID, Action<CBSBadgeResult> result)
        {
            var profileID = Profile.ProfileID;

            FabProfileTasks.GetProfileTasksBadge(profileID, tasksPoolID, onReset =>
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

        private void InternalModifyPoints(string tasksPoolID, string taskID, int points, ModifyMethod modify, Action<CBSModifyProfileTaskPointsResult> result)
        {
            var profileID = Profile.ProfileID;

            FabProfileTasks.ModifyTasksPoint(profileID, taskID, tasksPoolID, points, modify, onAdd =>
            {
                var cbsError = onAdd.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyProfileTaskPointsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onAdd.GetResult<FunctionModifyTaskResult<CBSProfileTask>>();
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
                        OnRewardGranted?.Invoke(reward);

                        var grantedInstances = reward.GrantedInstances;
                        if (grantedInstances != null && grantedInstances.Count > 0)
                        {
                            var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                            Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        }
                    }

                    if (complete)
                    {
                        OnCompleteTask?.Invoke(achievement);
                    }
                    if (completeTier)
                    {
                        OnCompleteTaskTier?.Invoke(achievement);
                    }

                    result?.Invoke(new CBSModifyProfileTaskPointsResult
                    {
                        IsSuccess = true,
                        Task = achievement,
                        ReceivedReward = reward,
                        CompleteTask = complete,
                        CompleteTier = completeTier
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyProfileTaskPointsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        private void InternalModifyMultiplyPoints(string tasksPoolID, ModifyMethod modify, Dictionary<string, int> modifyPair, Action<CBSModifyProfileMultiplyTaskResult> result)
        {
            var profileID = Profile.ProfileID;

            FabProfileTasks.ModifyTasksPoint(profileID, tasksPoolID, modify, modifyPair, onAdd =>
            {
                var cbsError = onAdd.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyProfileMultiplyTaskResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onAdd.GetResult<FunctionModifyMultiplyTaskResult<CBSProfileTask>>();

                    var tasksResult = new Dictionary<string, CBSModifyProfileTaskPointsResult>();
                    var functionTasksResult = functionResult.TasksResults;

                    foreach (var taskPair in functionTasksResult)
                    {
                        var taskResult = taskPair.Value;

                        var achievement = taskResult.Task;
                        var reward = taskResult.RewardResult;
                        var complete = taskResult.CompleteTask;
                        var completeTier = taskResult.CompleteTier;

                        if (functionResult != null && reward != null)
                        {
                            var currencies = reward.GrantedCurrencies;
                            if (currencies != null)
                            {
                                var codes = currencies.Select(x => x.Key).ToArray();
                                Get<CBSCurrencyModule>().ChangeRequest(codes);
                            }
                            OnRewardGranted?.Invoke(reward);

                            var grantedInstances = reward.GrantedInstances;
                            if (grantedInstances != null && grantedInstances.Count > 0)
                            {
                                var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                                Get<CBSInventoryModule>().AddRequest(inventoryItems);
                            }
                        }

                        if (complete)
                        {
                            OnCompleteTask?.Invoke(achievement);
                        }
                        if (completeTier)
                        {
                            OnCompleteTaskTier?.Invoke(achievement);
                        }

                        tasksResult[taskPair.Key] = new CBSModifyProfileTaskPointsResult 
                        {
                            IsSuccess = true,
                            Task = achievement,
                            ReceivedReward = reward,
                            CompleteTask = complete,
                            CompleteTier = completeTier
                        };
                    }      

                    result?.Invoke(new CBSModifyProfileMultiplyTaskResult
                    {
                        IsSuccess = true,
                        TasksResults = tasksResult
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyProfileMultiplyTaskResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }
    }
}
