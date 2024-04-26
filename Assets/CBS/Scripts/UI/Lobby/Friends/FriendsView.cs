using CBS.Models;
using UnityEngine;

namespace CBS.UI
{
    public class FriendsView : FriendsSection
    {
        [SerializeField]
        protected FriendsListScroller Scroller;

        public override FriendsTabTitle TabTitle => FriendsTabTitle.FRIENDS;

        private void Start()
        {
            Friends.OnFriendDeclined += OnFriendRemoved;
        }

        private void OnDestroy()
        {
            Friends.OnFriendDeclined -= OnFriendRemoved;
        }

        public override void Clean()
        {
            Scroller.HideAll();
        }

        public override void Display()
        {
            Friends.GetFriends(null, OnFriendsGet);
        }

        // events

        protected virtual void OnFriendsGet(CBSGetFriendsResult result)
        {
            if (result.IsSuccess)
            {
                var uiPrefab = Prefabs.FriendUI;
                var list = result.Friends;
                Scroller.Spawn(uiPrefab, list);
            }
        }

        private void OnFriendRemoved(string profileID)
        {
            Display();
        }
    }
}