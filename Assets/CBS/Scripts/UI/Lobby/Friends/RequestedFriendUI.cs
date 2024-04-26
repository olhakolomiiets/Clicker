using CBS.Models;

namespace CBS.UI
{
    public class RequestedFriendUI : FriendUI
    {
        public void AcceptFriend()
        {
            string userID = CurrentFriend.ProfileID;
            Friends.AcceptFriend(userID, OnActionWithFriend);
        }

        public void DeclineFriend()
        {
            string userID = CurrentFriend.ProfileID;
            Friends.DeclineFriendRequest(userID, OnActionWithFriend);
        }

        // event
        private void OnActionWithFriend(CBSActionWithFriendResult result)
        {
            if (!result.IsSuccess)
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
