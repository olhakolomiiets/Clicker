using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public interface IFabChat
    {
        void ClearDialogEntryBadge(string profileID, string withProfileID, Action<ExecuteFunctionResult> onClear, Action<PlayFabError> onFailed);
        void GetMessages(string chatID, int maxCount, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void SendMessage(FunctionSendChatMessageRequest messageRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void ChangeMessageText(string profileID, string messageID, string chatID, string textToEdit, Action<ExecuteFunctionResult> onEdit, Action<PlayFabError> onFailed);
        void DeleteMessageText(string profileID, string messageID, string chatID, Action<ExecuteFunctionResult> onEdit, Action<PlayFabError> onFailed);
        void BanProfileInChat(string profileID, string profileIDToBan, string chatID, int banHours, string reason, Action<ExecuteFunctionResult> onBan, Action<PlayFabError> onFailed);
        void GetStickersPack(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void ClaimItemFromMessage(string profileID, string messageID, string chatID, Action<ExecuteFunctionResult> onClaim, Action<PlayFabError> onFailed);
        void GetDialogList(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void GetDialogBadge(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
    }
}
