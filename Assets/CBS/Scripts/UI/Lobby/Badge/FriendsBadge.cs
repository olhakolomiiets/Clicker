using CBS.Models;

namespace CBS.UI
{
    public class FriendsBadge : BaseBadge
    {
        private IFriends Friends { get; set; }

        private int FriendsCount { get; set; }

        private void Awake()
        {
            Friends = CBSModule.Get<CBSFriendsModule>();
            Back.SetActive(false);
        }

        protected virtual void OnEnable()
        {
            Friends.GetFriendsBadge(OnGetBadge);

            // add listeners
            Friends.OnFriendAccepted += OnFriendAccepted;
            Friends.OnFriendDeclined += OnFriendDeclined;
            StartUpdateInterval();
        }

        protected virtual void OnDisable()
        {
            // remove listeners
            Friends.OnFriendAccepted -= OnFriendAccepted;
            Friends.OnFriendDeclined -= OnFriendDeclined;
            StopUpdateInterval();
        }

        private void OnGetBadge(CBSBadgeResult result)
        {
            if (result.IsSuccess)
            {
                UpdateCount(result.Count);
            }
        }

        // events
        private void OnFriendAccepted(string profileID)
        {
            Friends.GetFriendsBadge(OnGetBadge);
        }

        private void OnFriendDeclined(string profileID)
        {
            Friends.GetFriendsBadge(OnGetBadge);
        }

        protected override void OnUpdateInterval()
        {
            Friends.GetFriendsBadge(OnGetBadge);
        }
    }
}
