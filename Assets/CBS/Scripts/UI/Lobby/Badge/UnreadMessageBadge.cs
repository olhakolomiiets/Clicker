using CBS.Models;

namespace CBS.UI
{
    public class UnreadMessageBadge : BaseBadge
    {
        private ICBSChat CBSChat { get; set; }

        private void Awake()
        {
            CBSChat = CBSModule.Get<CBSChatModule>();
            Back.SetActive(false);
        }

        private void OnEnable()
        {
            CBSChat.GetProfileDialogBadge(OnGetDialogBadge);

            CBSChat.OnUnreadMessageClear += OnMessageClear;
        }

        private void OnDisable()
        {
            CBSChat.OnUnreadMessageClear -= OnMessageClear;
        }

        // events
        private void OnGetDialogBadge(CBSBadgeResult result)
        {
            if (result.IsSuccess)
            {
                var count = result.Count;
                UpdateCount(count);
            }
        }

        private void OnMessageClear(ChatDialogEntry dialogEntry)
        {
            CBSChat.GetProfileDialogBadge(OnGetDialogBadge);
        }
    }
}
