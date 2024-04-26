using CBS.Scriptable;
using System;
using UnityEngine;

namespace CBS.UI
{
    public abstract class FriendsSection : MonoBehaviour
    {
        private IFriends friends { get; set; }
        protected IFriends Friends
        {
            get
            {
                friends = friends ?? CBSModule.Get<CBSFriendsModule>();
                return friends;
            }
        }

        private ICBSChat chats { get; set; }
        protected ICBSChat Chats
        {
            get
            {
                chats = chats ?? CBSModule.Get<CBSChatModule>();
                return chats;
            }
        }

        private FriendsPrefabs prefabs { get; set; }
        protected FriendsPrefabs Prefabs
        {
            get
            {
                prefabs = prefabs ?? CBSScriptable.Get<FriendsPrefabs>();
                return prefabs;
            }
        }

        protected Action<string> SelectChatAction { get; set; }

        public abstract FriendsTabTitle TabTitle { get; }

        public abstract void Display();

        public abstract void Clean();

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetChatAction(Action<string> action)
        {
            SelectChatAction = action;
        }
    }
}
