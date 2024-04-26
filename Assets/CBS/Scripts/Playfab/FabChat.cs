using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabChat : FabExecuter, IFabChat
    {
        public void ClearDialogEntryBadge(string profileID, string withProfileID, Action<ExecuteFunctionResult> onClear, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ClearDialogBadgeMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = withProfileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onClear, onFailed);
        }

        public void GetMessages(string chatID, int maxCount, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetMessagesFromChatMethod,
                FunctionParameter = new GetChatMessagesRequest
                {
                    ChatID = chatID,
                    Count = maxCount
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void SendMessage(FunctionSendChatMessageRequest messageRequest, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.SendMessageToChatMethod,
                FunctionParameter = messageRequest
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void ChangeMessageText(string profileID, string messageID, string chatID, string textToEdit, Action<ExecuteFunctionResult> onEdit, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ModifyChatMessageMethod,
                FunctionParameter = new FunctionModifyChatMessageRequest
                {
                    ProfileID = profileID,
                    MessageID = messageID,
                    ChatID = chatID,
                    TextToEdit = textToEdit
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onEdit, onFailed);
        }

        public void DeleteMessageText(string profileID, string messageID, string chatID, Action<ExecuteFunctionResult> onEdit, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.DeleteChatMessageMethod,
                FunctionParameter = new FunctionChatMessageRequest
                {
                    ProfileID = profileID,
                    MessageID = messageID,
                    ChatID = chatID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onEdit, onFailed);
        }

        public void BanProfileInChat(string profileID, string profileIDToBan, string chatID, int banHours, string reason, Action<ExecuteFunctionResult> onBan, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.BanProfileInChatMethod,
                FunctionParameter = new ChatBanRequest
                {
                    ProfileID = profileID,
                    ChatID = chatID,
                    ProfileIDForBan = profileIDToBan,
                    BanHours = banHours,
                    Reason = reason
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onBan, onFailed);
        }

        public void GetStickersPack(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetChatStickersPackMethod,
                FunctionParameter = new FunctionBaseRequest()
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void ClaimItemFromMessage(string profileID, string messageID, string chatID, Action<ExecuteFunctionResult> onClaim, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ClaimItemFormMessageMethod,
                FunctionParameter = new FunctionChatMessageRequest
                {
                    ProfileID = profileID,
                    MessageID = messageID,
                    ChatID = chatID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onClaim, onFailed);
        }

        public void GetDialogList(string profileID, Action<ExecuteFunctionResult> onClaim, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetDialogListMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onClaim, onFailed);
        }

        public void GetDialogBadge(string profileID, Action<ExecuteFunctionResult> onClaim, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetDialogBadgeMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onClaim, onFailed);
        }
    }
}
