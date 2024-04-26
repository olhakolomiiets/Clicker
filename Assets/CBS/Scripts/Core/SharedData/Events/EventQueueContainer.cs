
using System.Collections.Generic;

namespace CBS.Models
{
    public class EventQueueContainer
    {
        public List<string> ExecuteMessages;
        public List<string> StartMessages;
        public List<string> StopMessages;

        public bool IsEventInQueue(string eventID)
        {
            return ExecuteMessages.Contains(eventID) || StartMessages.Contains(eventID) || StopMessages.Contains(eventID);
        }
    }
}
