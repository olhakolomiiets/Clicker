
namespace CBS.Models
{
    public class GetClanEntityRequest : FunctionBaseRequest
    {
        public string ClanID;
        public CBSClanConstraints Constraints;
    }
}
