using CBS.Models;
using System;
using System.Collections.Generic;

namespace CBS
{
    public interface IChat : IDisposable
    {
        event Action<ChatMessage> OnNewMessage;
        event Action<List<ChatMessage>> OnGetHistory;
        string ChatID { get; }
        string[] PrivateChatMembers { get; }

        void Load();
        void SetMaxMessagesCount(int maxMessagesToLoad);
        void SetUpdateIntervalMiliseconds(float updateInterval);
        void SendMessage(CBSSendTextMessageRequest request, Action<CBSSendChatMessageResult> result = null);
        void SendSticker(CBSSendStickerMessageRequest request, Action<CBSSendChatMessageResult> result = null);
        void SendItem(CBSSendItemMessageRequest request, Action<CBSSendChatMessageResult> result = null);
        void GetChatHistory(Action<CBSGetMessagesFromChatResult> result = null);
    }
}
