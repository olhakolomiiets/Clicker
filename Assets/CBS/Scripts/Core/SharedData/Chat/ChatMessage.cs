using System;

namespace CBS.Models
{
    public class ChatMessage
    {
        public string MessageID;
        public string ChatID;
        public MessageContent ContentType;
        public ChatTarget Target;
        public ChatAccess Visibility;
        public MessageState State;
        public ChatMember Sender;
        public ChatMember TaggedProfile;
        public DateTime CreationDateUTC;
        public string ContentRawData;
        public string CustomData;

        public string GetMessageBody()
        {
            return ContentRawData;
        }
    }
}

