
namespace CBS.Models
{
    public class FunctionGetProfilesDetailsRequest : FunctionBaseRequest
    {
        public string[] ProfilesIDs;
        public CBSProfileConstraints Constraints;
    }
}
