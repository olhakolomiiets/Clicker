
namespace CBS.Models
{
    public class CBSCheckFriendshipResult : CBSBaseResult
    {
        public string FriendID;
        public bool ExistAsAcceptedFriend;
        public bool ExistAsRequestedFriend;
        public bool WaitForProfileAccept;
    }
}
