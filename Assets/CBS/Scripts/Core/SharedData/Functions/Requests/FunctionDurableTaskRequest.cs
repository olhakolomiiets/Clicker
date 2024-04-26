

namespace CBS.Models
{
    public class FunctionDurableTaskRequest : FunctionBaseRequest
    {
        public string EventID;
        public object FunctionRequest;
        public string FunctionName;
        public long Delay;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(FunctionName) && !string.IsNullOrEmpty(EventID) && Delay > 0;
        }
    }
}
