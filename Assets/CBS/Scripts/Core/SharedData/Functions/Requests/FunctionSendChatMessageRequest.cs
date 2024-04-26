

namespace CBS.Models
{
    public class FunctionSendChatMessageRequest : FunctionBaseRequest
    {
        public string ChatID;
        public string SenderProfileID;
        public string ReceiverProfileID;
        public string InterlocutorProfileID;
        public MessageContent ContentType;
        public ChatTarget Target;
        public ChatAccess Visibility;
        public string CustomData;

        public string MessageBody;
        public string ItemInstanceID;
        public string StickerID;

        public bool IsValidInput()
        {
            if (string.IsNullOrEmpty(ChatID))
                return false;
            if (Target == ChatTarget.DEFAULT && string.IsNullOrEmpty(SenderProfileID))
                return false;
            if (ContentType == MessageContent.MESSAGE && string.IsNullOrEmpty(MessageBody))
                return false;
            if (ContentType == MessageContent.ITEM && string.IsNullOrEmpty(ItemInstanceID))
                return false;
            if (ContentType == MessageContent.STICKER && string.IsNullOrEmpty(StickerID))
                return false;
            return true;
        }
    }
}
