
namespace CBS.Models
{
    public class FunctionChangeRoleRequest : FunctionBaseRequest
    {
        public string ClanID;
        public string ProfileIDToChange;
        public string NewRoleID;
    }
}

