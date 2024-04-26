using CBS.Models;
using CBS.Utils;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;
using System.Collections.Generic;

namespace CBS.Playfab
{
    public class FabProfileTasks : FabExecuter, IFabProfileTasks
    {
        public void GetTasksPool(string profileID, string tasksPoolID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileTasksPoolMethod,
                FunctionParameter = new FunctionProfileTasksRequest
                {
                    ProfileID = profileID,
                    TasksPoolID = tasksPoolID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetTasksForProfile(string profileID, string tasksPoolID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetTasksForProfileMethod,
                FunctionParameter = new FunctionProfileTasksRequest
                {
                    ProfileID = profileID,
                    TimeZone = DateUtils.GetZoneOffset(),
                    TasksPoolID = tasksPoolID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void ModifyTasksPoint(string profileID, string taskID, string tasksPoolID, int points, ModifyMethod method, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddProfileTaskPointsMethod,
                FunctionParameter = new FunctionModifyProfileTasksPointsRequest
                {
                    ProfileID = profileID,
                    Points = points,
                    Method = method,
                    TaskID = taskID,
                    TasksPoolID = tasksPoolID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void ModifyTasksPoint(string profileID, string tasksPoolID, ModifyMethod method, Dictionary<string, int> modifyPair, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddProfileMuliplyTaskPointsMethod,
                FunctionParameter = new FunctionModifyProfileMultiplyTasksPointsRequest
                {
                    ProfileID = profileID,
                    Method = method,
                    TasksPoolID = tasksPoolID,
                    ModifyPair = modifyPair,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void PickupReward(string profileID, string taskID, string tasksPoolID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PickupProfileTaskRewardMethod,
                FunctionParameter = new FunctionProfileTasksRequest
                {
                    ProfileID = profileID,
                    TaskID = taskID,
                    TasksPoolID = tasksPoolID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void RegenerateNewTasks(string profileID, string tasksPoolID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ResetProfileTasksMethod,
                FunctionParameter = new FunctionProfileTasksRequest
                {
                    ProfileID = profileID,
                    TasksPoolID = tasksPoolID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetProfileTasksBadge(string profileID, string tasksPoolID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileTasksBadgeMethod,
                FunctionParameter = new FunctionProfileTasksRequest
                {
                    ProfileID = profileID,
                    TasksPoolID = tasksPoolID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }
    }
}