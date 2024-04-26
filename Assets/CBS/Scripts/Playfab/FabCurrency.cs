using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabCurrency : FabExecuter, IFabCurrency
    {
        public void AddProfileCurrerncy(string profileID, string code, int amount, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddProfileCurrencyMethod,
                FunctionParameter = new FunctionChangeCurrencyRequest
                {
                    ProfileID = profileID,
                    Code = code,
                    Amount = amount
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void SubtractProfileCurrerncy(string profileID, string code, int amount, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.SubtractProfileCurrencyMethod,
                FunctionParameter = new FunctionChangeCurrencyRequest
                {
                    ProfileID = profileID,
                    Code = code,
                    Amount = amount
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void GetProfileCurrencies(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileCurrencyMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetCurrenciesPacks(string tag, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetCurrenciesPackMethod,
                FunctionParameter = new FunctionKeyRequest
                {
                    Key = tag
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GrantCurrencyPack(string profileID, string packID, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GrantCurrencyPackMethod,
                FunctionParameter = new FunctionGrantItemRequest
                {
                    ProfileID = profileID,
                    ItemID = packID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGrant, onFailed);
        }
    }
}
