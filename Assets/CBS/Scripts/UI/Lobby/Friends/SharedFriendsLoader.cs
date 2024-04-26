using CBS.Models;
using CBS.Scriptable;
using UnityEngine;

namespace CBS.UI
{
    public class SharedFriendsLoader : MonoBehaviour
    {
        [SerializeField]
        private FriendsListScroller Scroller;

        private FriendsPrefabs Prefabs { get; set; }
        private IFriends Friends { get; set; }

        private void Awake()
        {
            Prefabs = CBSScriptable.Get<FriendsPrefabs>();
            Friends = CBSModule.Get<CBSFriendsModule>();
        }

        public void LoadFriends(string profileID)
        {
            Scroller.HideAll();
            Friends.GetSharedFriendsWithProfile(profileID, null, OnFriendsGet);
        }

        // events
        protected virtual void OnFriendsGet(CBSGetFriendsResult result)
        {
            if (result.IsSuccess)
            {
                var uiPrefab = Prefabs.SharedFriendUI;
                var list = result.Friends;
                Scroller.Spawn(uiPrefab, list);
            }
        }
    }
}
