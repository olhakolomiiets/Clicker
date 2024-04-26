using CBS.Models;
using CBS.Utils;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabCalendar : FabExecuter, IFabCalendar
    {
        public void GetAllAvailableCalendars(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetAllCalendarsMethod,
                FunctionParameter = new FunctionCalendarRequest
                {
                    ProfileID = profileID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetCalendarByID(string profileID, string calendarID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetCalendarMethod,
                FunctionParameter = new FunctionCalendarRequest
                {
                    ProfileID = profileID,
                    CalendarID = calendarID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void PickupReward(string profileID, string calendarID, int index, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PickupCalendarRewardMethod,
                FunctionParameter = new FunctionCalendarRewardRequest
                {
                    ProfileID = profileID,
                    CalendarID = calendarID,
                    Index = index,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void ResetCalendar(string profileID, string calendarID, Action<ExecuteFunctionResult> onReset, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ResetCalendarMethod,
                FunctionParameter = new FunctionCalendarRequest
                {
                    ProfileID = profileID,
                    CalendarID = calendarID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onReset, onFailed);
        }

        public void GetCalendarBadge(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetCalendarBadgeMethod,
                FunctionParameter = new FunctionCalendarRequest
                {
                    ProfileID = profileID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GrantCalendar(string profileID, string calendarID, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GrantCalendarMethod,
                FunctionParameter = new FunctionCalendarRequest
                {
                    ProfileID = profileID,
                    CalendarID = calendarID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGrant, onFailed);
        }

        public void PrePurchaseValidation(string profileID, string calendarID, Action<ExecuteFunctionResult> onValidate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PreCalendarPurchaseProccessMethod,
                FunctionParameter = new FunctionCalendarRequest
                {
                    ProfileID = profileID,
                    CalendarID = calendarID,
                    TimeZone = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onValidate, onFailed);
        }

        public void PurchaseCalendar(string calendarID, string currencyCode, int currencyValue, Action<PurchaseItemResult> onPurchase, Action<PlayFabError> onFailed)
        {
            var request = new PurchaseItemRequest
            {
                ItemId = calendarID,
                VirtualCurrency = currencyCode,
                Price = currencyValue,
                CatalogVersion = CatalogKeys.CalendarCatalogID,
            };
            PlayFabClientAPI.PurchaseItem(request, onPurchase, onFailed);
        }
    }
}
