using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public interface IFabBattlePass
    {
        void GetProfileBattlePassStates(string profileID, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void GetBattlePassFullInformation(string profileID, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void AddExpirienceToInstance(string profileID, int exp, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void GetRewardFromInstance(string profileID, string battlePassID, int level, bool IsPremiun, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void GetPremiumAccess(string profileID, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void ResetInstanceForProfile(string profileID, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void PrePurchaseValidation(string profileID, string battlePassID, string ticketID, Action<ExecuteFunctionResult> onValidate, Action<PlayFabError> onFailed);
        void PostPurchaseValidation(string profileID, string battlePassID, string ticketID, Action<ExecuteFunctionResult> onValidate, Action<PlayFabError> onFailed);
        void PurchaseTicket(string ticketCatalogID, string currencyCode, int currencyValue, Action<PurchaseItemResult> onPurchase, Action<PlayFabError> onFailed);
        void GrantTicket(string profileID, string battlePassID, string ticketID, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed);
        void GetBattlePassTasks(string profileID, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void AddBattlePassTaskPoints(string profileID, string battlePassID, string taskID, int points, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed);
        void PickupBattlePassTaskReward(string profileID, string battlePassID, string taskID, Action<ExecuteFunctionResult> onPick, Action<PlayFabError> onFailed);
    }
}
