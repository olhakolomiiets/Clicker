using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;

public interface IFabCalendar
{
    void GetAllAvailableCalendars(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

    void GetCalendarByID(string profileID, string calendarID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

    void PickupReward(string profileID, string calendarID, int index, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

    void ResetCalendar(string profileID, string calendarID, Action<ExecuteFunctionResult> onReset, Action<PlayFabError> onFailed);

    void GetCalendarBadge(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

    void GrantCalendar(string profileID, string calendarID, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed);

    void PrePurchaseValidation(string profileID, string calendarID, Action<ExecuteFunctionResult> onValidate, Action<PlayFabError> onFailed);

    void PurchaseCalendar(string calendarID, string currencyCode, int currencyValue, Action<PurchaseItemResult> onPurchase, Action<PlayFabError> onFailed);
}
