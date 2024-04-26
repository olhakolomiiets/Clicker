using CBS.Scriptable;
using UnityEngine;

namespace CBS.UI
{
    public class PrivateMessageView : MonoBehaviour
    {
        [SerializeField]
        private ChatView ChatView;

        private ICBSChat cbsChat;
        private ICBSChat CBSChat
        {
            get
            {
                if (cbsChat == null)
                    cbsChat = CBSModule.Get<CBSChatModule>();
                return cbsChat;
            }
        }
        private ChatPrefabs prefabs { get; set; }
        protected ChatPrefabs Prefabs
        {
            get
            {
                prefabs = prefabs ?? CBSScriptable.Get<ChatPrefabs>();
                return prefabs;
            }
        }

        private string UserID { get; set; }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(UserID))
            {
                CBSChat.ClearDialogBadgeWithProfile(UserID, result => { });
            }
        }

        public void Setup(string userID)
        {
            UserID = userID;
            var activeChat = CBSChat.GetOrCreatePrivateChatWithProfile(userID);
            //ChatView.SetMessagePrefab(Prefabs.BubbleChatMessage);
            ChatView.Init(activeChat);
            CBSChat.ClearDialogBadgeWithProfile(userID, result => { });
        }
    }
}
