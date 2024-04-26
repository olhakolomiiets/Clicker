using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabTitleData : FabExecuter, IFabTitle
    {
        public void GetAllTitleData(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetAllTitleDataMethod,
                FunctionParameter = new FunctionBaseRequest()
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetTitleDataByKey(string key, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetTitleDataByKeyMethod,
                FunctionParameter = new FunctionKeyRequest
                {
                    Key = key
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }
    }
}
