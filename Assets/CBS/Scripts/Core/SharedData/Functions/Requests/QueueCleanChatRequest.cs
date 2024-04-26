
namespace CBS.Models
{
    public class QueueCleanChatRequest : FunctionBaseRequest
    {
        public ChatAccess Access;
        public int TTL;
        public int SaveLastCount;
    }
}
