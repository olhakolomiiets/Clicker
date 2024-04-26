using CBS.Models;
using CBS.Utils;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabBattlePass : FabExecuter, IFabBattlePass
    {
        public void GetProfileBattlePassStates(string profileID, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileBattlePassStatesMethod,
                FunctionParameter = new FunctionBattlePassRequest
                {
                    ProfileID = profileID,
                    BattlePassID = battlePassID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetBattlePassFullInformation(string profileID, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetBattlePassFullInformationMethod,
                FunctionParameter = new FunctionBattlePassRequest
                {
                    ProfileID = profileID,
                    BattlePassID = battlePassID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void AddExpirienceToInstance(string profileID, int exp, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddBattlePassExpirienceMethod,
                FunctionParameter = new FunctionAddPassExpRequest
                {
                    ProfileID = profileID,
                    BattlePassID = battlePassID,
                    ExpToAdd = exp,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetRewardFromInstance(string profileID, string battlePassID, int level, bool IsPremium, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetRewardFromBattlePassInstanceMethod,
                FunctionParameter = new FunctionGetPassRewardRequest
                {
                    ProfileID = profileID,
                    BattlePassID = battlePassID,
                    LevelIndex = level,
                    IsPremium = IsPremium,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetPremiumAccess(string profileID, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GrantPremiumAccessToBattlePassMethod,
                FunctionParameter = new FunctionBattlePassRequest
                {
                    ProfileID = profileID,
                    BattlePassID = battlePassID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void ResetInstanceForProfile(string profileID, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ResetPlayerStateForBattlePassMethod,
                FunctionParameter = new FunctionBattlePassRequest
                {
                    ProfileID = profileID,
                    BattlePassID = battlePassID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void PrePurchaseValidation(string profileID, string battlePassID, string ticketID, Action<ExecuteFunctionResult> onValidate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PreTicketPurchaseProccessMethod,
                FunctionParameter = new FunctionTicketRequest
                {
                    ProfileID = profileID,
                    TicketID = ticketID,
                    BattlePassID = battlePassID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onValidate, onFailed);
        }

        public void PostPurchaseValidation(string profileID, string battlePassID, string ticketID, Action<ExecuteFunctionResult> onValidate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PostTicketPurchaseProccessMethod,
                FunctionParameter = new FunctionTicketRequest
                {
                    ProfileID = profileID,
                    TicketID = ticketID,
                    BattlePassID = battlePassID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onValidate, onFailed);
        }

        public void PurchaseTicket(string ticketCatalogID, string currencyCode, int currencyValue, Action<PurchaseItemResult> onPurchase, Action<PlayFabError> onFailed)
        {
            var request = new PurchaseItemRequest
            {
                ItemId = ticketCatalogID,
                VirtualCurrency = currencyCode,
                Price = currencyValue,
                CatalogVersion = CatalogKeys.BattlePassCatalogID,
            };
            PlayFabClientAPI.PurchaseItem(request, onPurchase, onFailed);
        }

        public void GrantTicket(string profileID, string battlePassID, string ticketID, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GrantTicketMethod,
                FunctionParameter = new FunctionTicketRequest
                {
                    ProfileID = profileID,
                    TicketID = ticketID,
                    BattlePassID = battlePassID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGrant, onFailed);
        }

        public void GetBattlePassTasks(string profileID, string battlePassID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetBattlePassTasksMethod,
                FunctionParameter = new FunctionBattlePassTasksRequest
                {
                    ProfileID = profileID,
                    BattlePassID = battlePassID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void AddBattlePassTaskPoints(string profileID, string battlePassID, string taskID, int points, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddBattlePassTaskPointsMethod,
                FunctionParameter = new FunctionBattlePassAddTaskPointsRequest
                {
                    ProfileID = profileID,
                    TaskID = taskID,
                    BattlePassID = battlePassID,
                    Points = points,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onAdd, onFailed);
        }

        public void PickupBattlePassTaskReward(string profileID, string battlePassID, string taskID, Action<ExecuteFunctionResult> onPick, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PickupBattlePassTaskRewardMethod,
                FunctionParameter = new FunctionBattlePassTasksRequest
                {
                    ProfileID = profileID,
                    BattlePassID = battlePassID,
                    TaskID = taskID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onPick, onFailed);
        }
    }
}
