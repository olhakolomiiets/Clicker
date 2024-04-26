using CBS.Core;
using CBS.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Toggle))]
    public class DialogUI : MonoBehaviour, IScrollableItem<DialogSlotRequest>
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Message;
        [SerializeField]
        private Text BadgeCount;
        [SerializeField]
        private GameObject BadgeBody;
        [SerializeField]
        private AvatarDrawer Avatar;

        private Toggle Toggle { get; set; }
        private ChatDialogEntry Dialog { get; set; }
        private Action<string> ClickAction { get; set; }
        private Action<ChatDialogEntry> SelectAction { get; set; }

        private ICBSChat CBSChat { get; set; }

        private void Awake()
        {
            Toggle = GetComponent<Toggle>();
            CBSChat = CBSModule.Get<CBSChatModule>();
            CBSChat.OnUnreadMessageClear += OnUnreadMessageClear;
            Toggle.onValueChanged.AddListener(OnToggleChange);
        }

        private void OnDisable()
        {
            Toggle.isOn = false;
        }

        private void OnDestroy()
        {
            CBSChat.OnUnreadMessageClear -= OnUnreadMessageClear;
            Toggle.onValueChanged.RemoveListener(OnToggleChange);
        }

        public void Display(DialogSlotRequest data)
        {
            Toggle.group = data.Group;
            var entry = data.DialogEntry;
            Dialog = entry;
            SelectAction = data.SelectAction;
            // draw ui
            var profile = Dialog.InterlocutorProfile;
            var profileID = profile.ProfileID;
            DisplayName.text = profile.DisplayName;
            Message.text = Dialog.LastMessage.GetMessageBody();
            var avatarInfo = profile.Avatar;
            Avatar.LoadProfileAvatar(profileID, avatarInfo);
            Avatar.SetClickable(profileID);

            DrawBadge();
        }

        private void DrawBadge()
        {
            int unreadCount = Dialog.BadgeCount;
            BadgeBody.SetActive(unreadCount > 0);
            BadgeCount.text = unreadCount.ToString();
        }

        public void OnClickDialog()
        {
            ClickAction?.Invoke(Dialog.InterlocutorProfile.ProfileID);
        }

        public void SetClickAction(Action<string> action)
        {
            ClickAction = action;
        }

        // events
        private void OnUnreadMessageClear(ChatDialogEntry dialogEntry)
        {
            if (Dialog == null)
                return;
            if (Dialog.InterlocutorProfile.ProfileID == dialogEntry.InterlocutorProfile.ProfileID)
            {
                Dialog = dialogEntry;
                DrawBadge();
            }
        }

        private void OnToggleChange(bool val)
        {
            if (val)
            {
                SelectAction?.Invoke(Dialog);
            }
        }
    }
}
