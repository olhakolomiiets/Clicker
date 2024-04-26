using System;
using UnityEngine;

namespace CBS.UI
{
    public class ClanChat : MonoBehaviour, IClanScreen
    {
        [SerializeField]
        private ChatView ChatView;

        private IProfile Profile { get; set; }
        private ICBSChat Chat { get; set; }

        public Action OnBack { get; set; }

        private void Awake()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
            Chat = CBSModule.Get<CBSChatModule>();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            LoadChat();
        }

        public void BackHandler()
        {
            ChatView.Dispose();
            OnBack?.Invoke();
        }

        private void LoadChat()
        {
            var clanID = Profile.ClanID;
            var chatInstsance = Chat.GetOrCreateGroupChatByID(clanID);
            ChatView.Init(chatInstsance);
        }
    }
}
