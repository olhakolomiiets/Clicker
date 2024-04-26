using CBS.Context;
using CBS.Models;
using System;
using System.Collections.Generic;

namespace CBS
{
    public interface ICBSChat
    {
        /// <summary>
        /// Notify when the current user has cleared the conversation from unread messages.
        /// </summary>
        event Action<ChatDialogEntry> OnUnreadMessageClear;

        /// <summary>
        /// Determines if the authorized profile is a moderator
        /// </summary>
        bool IsModerator { get; }

        /// <summary>
        /// Information about profile for chats. Contain information about ban list
        /// </summary>
        ProfileChatData ProfileChatData { get; }

        /// <summary>
        /// Unity context for running coroutines
        /// </summary>
        ICoroutineRunner CoroutineRunner { get; }

        /// <summary>
        /// Sets the server id for the server chat. You need to set this value before initializing the chat.
        /// </summary>
        /// <param name="serverID"></param>
        void SetServerID(string serverID);

        /// <summary>
        /// Sets the region ID for the regional chat. You need to set this value before initializing the chat. For example "ru", "en".
        /// </summary>
        /// <param name="region"></param>
        void SetRegion(string region);

        /// <summary>
        /// Get chat instance from pre-configured template. Global, Server, Regional.
        /// </summary>
        /// <param name="chatTitle"></param>
        /// <returns></returns>
        ChatInstance GetOrCreateChat(ChatTitle chatTitle);

        /// <summary>
        /// Get chat instance from custom id. Suitable for creating group chats for example.
        /// </summary>
        /// <param name="chatID"></param>
        /// <returns></returns>
        ChatInstance GetOrCreateGroupChatByID(string chatID);

        /// <summary>
        /// Get private chat instance from custom id. Suitable for private chats, from lists of unread messages.
        /// </summary>
        /// <param name="withProfileID"></param>
        /// <returns></returns>
        ChatInstance GetOrCreatePrivateChatWithProfile(string withProfileID);

        /// <summary>
        /// Get list of main chat titles.
        /// </summary>
        /// <returns></returns>
        List<ChatTitle> GetChatTitles();

        /// <summary>
        /// Get unique id for chat between two profiles
        /// </summary>
        /// <param name="profileID1"></param>
        /// <param name="profileID2"></param>
        /// <returns></returns>
        string GetChatIdBetweenTwoProfiles(string profileID1, string profileID2);

        /// <summary>
        /// Get a list of the current player's conversations with whom there was previously a private conversation.
        /// </summary>
        /// <param name="result"></param>
        void GetProfileDialogList(Action<CBSGetDialogListResult> result);

        /// <summary>
        /// Get count of unread private messages
        /// </summary>
        /// <param name="result"></param>
        void GetProfileDialogBadge(Action<CBSBadgeResult> result);

        /// <summary>
        /// Clears the list of unread messages with a specific user.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void ClearDialogBadgeWithProfile(string profileID, Action<CBSClearDialogBadgeResult> result);

        /// <summary>
        /// Get messages from chat history. Max count = 1000
        /// </summary>
        /// <param name="chatID"></param>
        /// <param name="maxCount"></param>
        /// <param name="result"></param>
        void GetMessagesFromChat(string chatID, int maxCount, Action<CBSGetMessagesFromChatResult> result);

        /// <summary>
        /// Send text message to group chat
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        void SendTextMessageToGroupChat(string chatID, CBSSendTextMessageRequest messageRequest, Action<CBSSendChatMessageResult> result);

        /// <summary>
        /// Send sticker to group chat.
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        void SendStickerMessageToGroupChat(string chatID, CBSSendStickerMessageRequest messageRequest, Action<CBSSendChatMessageResult> result);

        /// <summary>
        /// Send item to group chat. Item can only be picked up by one chat member
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        void SendItemMessageToGroupChat(string chatID, CBSSendItemMessageRequest messageRequest, Action<CBSSendChatMessageResult> result);

        /// <summary>
        /// Send text message to private chat between two profiles
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        void SendTextMessageToPrivateChat(string withProfileID, CBSSendTextMessageRequest messageRequest, Action<CBSSendChatMessageResult> result);

        /// <summary>
        /// Send sticker to private chat between two profiles
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        void SendStickerMessageToPrivateChat(string withProfileID, CBSSendStickerMessageRequest messageRequest, Action<CBSSendChatMessageResult> result);

        /// <summary>
        /// Send item to private chat between two profiles. Item can only be picked up by one chat member
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        void SendItemMessageToPrivateChat(string withProfileID, CBSSendItemMessageRequest messageRequest, Action<CBSSendChatMessageResult> result);

        /// <summary>
        /// Change message text property. Doesn't work with "Items" or "Stickers" messages
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="chatID"></param>
        /// <param name="textToChange"></param>
        /// <param name="result"></param>
        void ChangeMessageText(string messageID, string chatID, string textToChange, Action<CBSModifyMessageResult> result);

        /// <summary>
        /// Mark chat message as deleted. Only the owner or moderator can delete a message
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="chatID"></param>
        /// <param name="result"></param>
        void DeleteChatMessage(string messageID, string chatID, Action<CBSModifyMessageResult> result);

        /// <summary>
        /// Mute profile in chat for a time. Action available only for moderators 
        /// </summary>
        /// <param name="profileIDForBan"></param>
        /// <param name="chatID"></param>
        /// <param name="reason"></param>
        /// <param name="banHours"></param>
        /// <param name="result"></param>
        void BanProfile(string profileIDForBan, string chatID, string reason, int banHours, Action<CBSBanProfileInChatResult> result);

        /// <summary>
        /// Get list of available stickers
        /// </summary>
        /// <param name="result"></param>
        void GetStickersPack(Action<CBSGetStickersResult> result);

        /// <summary>
        /// Claim item from message if available.
        /// </summary>
        /// <param name="chatID"></param>
        /// <param name="messageID"></param>
        /// <param name="result"></param>
        void ClaimItemFromChat(string chatID, string messageID, Action<CBSClaimItemFromMessageResult> result);
    }
}
