using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabEvents : FabExecuter, IFabEvents
    {
        public void GetEventsList(bool activeonly, string specificCategory, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetCBSEventsMethod,
                FunctionParameter = new FunctionGetCBSEventsRequest
                {
                    ActiveOnly = activeonly,
                    ByCategory = specificCategory
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetEventByID(string eventID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetCBSEventByIDMethod,
                FunctionParameter = new FunctionGetCBSEventRequest
                {
                    EventID = eventID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetEventsBadge(string category, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetEventBadgeMethod,
                FunctionParameter = new FunctionGetCBSEventsRequest
                {
                    ByCategory = category
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }
    }
}
