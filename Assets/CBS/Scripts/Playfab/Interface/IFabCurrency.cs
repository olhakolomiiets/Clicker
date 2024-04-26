using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public interface IFabCurrency
    {
        void AddProfileCurrerncy(string profileID, string code, int amount, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void SubtractProfileCurrerncy(string profileID, string code, int amount, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void GetProfileCurrencies(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetCurrenciesPacks(string tag, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GrantCurrencyPack(string profileID, string packID, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed);
    }
}
