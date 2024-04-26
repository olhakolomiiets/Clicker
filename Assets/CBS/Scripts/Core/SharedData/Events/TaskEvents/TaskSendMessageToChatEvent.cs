
namespace CBS.Models
{
    public class TaskSendMessageToChatEvent : TaskEvent
    {
        public string ChatID;
        public string ChatMessage;
    }
}
