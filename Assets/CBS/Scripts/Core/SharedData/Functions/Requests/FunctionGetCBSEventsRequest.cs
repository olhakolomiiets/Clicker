

namespace CBS.Models
{
    public class FunctionGetCBSEventsRequest : FunctionBaseRequest
    {
        public bool ActiveOnly;
        public string ByCategory;
    }
}
