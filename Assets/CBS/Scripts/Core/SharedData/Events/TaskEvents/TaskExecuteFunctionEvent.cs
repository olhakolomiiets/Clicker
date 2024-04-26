
namespace CBS.Models
{
    public class TaskExecuteFunctionEvent : TaskEvent
    {
        public string FunctionName;
        public string RequestRaw;
    }
}
