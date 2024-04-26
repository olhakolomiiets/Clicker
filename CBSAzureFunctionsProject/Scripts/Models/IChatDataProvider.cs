using System.Collections.Generic;
using System.Threading.Tasks;
using CBS.Models;

namespace CBS
{
    public interface IChatDataProvider
    {
        Task<ExecuteResult<List<ProfileEntity>>> GetModeratorsAsync();

        Task<ExecuteResult<FunctionEmptyResult>> AddToModeratorsListAsync(string profileID, string displayName);

        Task<ExecuteResult<FunctionEmptyResult>> RemoveFromModeratorsListAsync(string profileID);

        Task<ExecuteResult<List<ChatMessage>>> GetMessagesFromChatAsync(string chatID, int count);

        Task<ExecuteResult<FunctionEmptyResult>> SendMessageAsync(string chatID, ChatMessage message);

        Task<ExecuteResult<FunctionEmptyResult>> UpdateChatActivityAsync(string chatID, ChatAccess access, string messageID);

        Task<ExecuteResult<FunctionEmptyResult>> CleanUpChatsAsync(ChatAccess access, int ttl, int saveLast);

        Task<ExecuteResult<ChatMessage>> GetMessageByIDAsync(string messageID, string chatID);

        Task<ExecuteResult<FunctionEmptyResult>> ChangeMessageAsync(string messageID, string chatID, ChatMessage message);

        Task<ExecuteResult<ChatDialogEntry>> ClearDialogBadgeWithProfileAsync(string ownerID, string interlocutorID);

        Task<ExecuteResult<FunctionEmptyResult>> UpdateDialogListWithNewMessageAsync(ChatMember sender, ChatMember interlocutor, ChatMessage message);

        Task<ExecuteResult<FunctionGetDialogListResult>> GetDialogListAsync(string profileID, int count);
        
        string GenerateNextMessageID();
    }
}