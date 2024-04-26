using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;
using System.Collections.Generic;

namespace CBS.Playfab
{
    public interface IFabProfileTasks
    {
        void GetTasksPool(string profileID, string tasksPoolID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetTasksForProfile(string profileID, string tasksPoolID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void ModifyTasksPoint(string profileID, string taskID, string tasksPoolID, int points, ModifyMethod method, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void ModifyTasksPoint(string profileID, string tasksPoolID, ModifyMethod method, Dictionary<string, int> modifyPair, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void PickupReward(string profileID, string taskID, string tasksPoolID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void RegenerateNewTasks(string profileID, string tasksPoolID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetProfileTasksBadge(string profileID, string tasksPoolID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
    }
}
