

namespace CBS.Models
{
    public class FunctionGetClanProfilesRequest : FunctionBaseRequest
    {
        public string ClanID;
        public CBSProfileConstraints Constraints;
    }
}
