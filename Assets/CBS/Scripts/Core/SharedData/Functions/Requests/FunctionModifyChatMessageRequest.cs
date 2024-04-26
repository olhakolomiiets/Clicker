

namespace CBS.Models
{
    public class FunctionModifyChatMessageRequest : FunctionBaseRequest
    {
        public string MessageID;
        public string ChatID;
        public string TextToEdit;
    }
}
