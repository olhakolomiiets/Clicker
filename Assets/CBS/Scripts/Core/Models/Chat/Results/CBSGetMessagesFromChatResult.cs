
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetMessagesFromChatResult : CBSBaseResult
    {
        public string ChatID;
        public List<ChatMessage> Messages;
    }
}
