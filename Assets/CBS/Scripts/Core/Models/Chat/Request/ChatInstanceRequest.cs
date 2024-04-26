
namespace CBS.Models
{
    public class ChatInstanceRequest
    {
        public string ChatID;
        public ChatAccess Access;
        public string[] PrivateChatMembers;
        public int LoadMessagesCount;
    }
}
