

namespace CBS.Models
{
    public class GetChatMessagesRequest : FunctionBaseRequest
    {
        public string ChatID;
        public int Count;
    }
}
