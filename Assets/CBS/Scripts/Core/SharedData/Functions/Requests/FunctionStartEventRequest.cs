using CBS.Models;

namespace CBS
{
    public class FunctionStartEventRequest : FunctionBaseRequest
    {
        public string EventID;
        public bool Manual;
    }
}
