using CBS.Models;

namespace CBS
{
    public class FunctionStopEventRequest : FunctionBaseRequest
    {
        public string EventID;
        public bool Manual;
    }
}
