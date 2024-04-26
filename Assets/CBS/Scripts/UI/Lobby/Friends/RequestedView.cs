using CBS.Models;
using UnityEngine;

namespace CBS.UI
{
    public class RequestedView : FriendsSection
    {
        [SerializeField]
        private FriendsListScroller Scroller;

        public override FriendsTabTitle TabTitle => FriendsTabTitle.REQUESTED;

        private void Start()
        {
            Friends.OnFriendDeclined += OnFriendDeclined;
            Friends.OnFriendAccepted += OnFriendAccepted;
        }

        private void OnDestroy()
        {
            Friends.OnFriendDeclined -= OnFriendDeclined;
            Friends.OnFriendAccepted -= OnFriendAccepted;
        }

        public override void Clean()
        {
            Scroller.HideAll();
        }

        public override void Display()
        {
            Friends.GetRequestedFriends(null, OnFriendsGet);
        }

        // events
        private void OnFriendsGet(CBSGetFriendsResult result)
        {
            var uiPrefab = Prefabs.RequestedFriendUI;
            var list = result.Friends;
            Scroller.Spawn(uiPrefab, list);
        }

        private void OnFriendDeclined(string profileID)
        {
            Display();
        }

        private void OnFriendAccepted(string profileID)
        {
            Display();
        }
    }
}
