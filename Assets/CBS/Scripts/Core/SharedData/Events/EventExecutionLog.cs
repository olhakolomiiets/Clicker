using System;

namespace CBS.Models
{
    public class EventExecutionLog
    {
        public string EventID;
        public string EventName;
        public bool IsSuccess;
        public string Message;
        public DateTime? LogDate;
    }
}