using CBS.Models;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class MessagePopup : MonoBehaviour
    {
        [SerializeField]
        private AvatarDrawer Avatar;
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text MessageContent;
        [SerializeField]
        private InputField EditField;
        [SerializeField]
        private InputField HoursField;
        [SerializeField]
        private InputField ReasonField;
        [SerializeField]
        private GameObject ViewProfileButton;
        [SerializeField]
        private GameObject EditButton;
        [SerializeField]
        private GameObject SaveButton;
        [SerializeField]
        private GameObject DeleteButton;
        [SerializeField]
        private GameObject BanButton;
        [SerializeField]
        private GameObject ConfirmBanButton;
        [SerializeField]
        private GameObject BanPanel;

        private ChatMessage Message { get; set; }
        private Action<ChatMessage> ModifyAction { get; set; }
        private IProfile Profile { get; set; }
        private ICBSChat CBSChat { get; set; }
        private bool IsEditMode { get; set; }
        private bool IsBanMode { get; set; }

        private void Awake()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
            CBSChat = CBSModule.Get<CBSChatModule>();
        }

        public void Show(ChatMessage message, Action<ChatMessage> onModify = null)
        {
            Message = message;
            ModifyAction = onModify;
            DisplayPopup();
        }

        private void DisplayPopup()
        {
            IsEditMode = false;
            IsBanMode = false;
            EditField.text = string.Empty;
            EditField.gameObject.SetActive(false);
            BanPanel.SetActive(false);
            MessageContent.gameObject.SetActive(true);

            var isMine = IsMine();
            var target = Message.Target;
            var sender = Message.Sender;
            var content = Message.ContentType;
            var isText = Message.ContentType == Models.MessageContent.MESSAGE;

            ViewProfileButton.SetActive(!isMine && target == ChatTarget.DEFAULT && sender != null);
            SaveButton.SetActive(false);
            ConfirmBanButton.SetActive(false);
            EditButton.SetActive(isMine && isText);
            DeleteButton.SetActive(isMine || CBSChat.IsModerator);
            BanButton.SetActive(CBSChat.IsModerator && !isMine);

            if (target == ChatTarget.DEFAULT)
            {
                var senderName = sender.DisplayName;
                var sernderID = sender.ProfileID;
                DisplayName.text = senderName;
                var avatarInfo = sender.Avatar;
                Avatar.LoadProfileAvatar(sernderID, avatarInfo);
            }
            else
            {
                DisplayName.text = ChatUtils.SystemMessageTitle;
                Avatar.DisplayDefaultAvatar();
            }

            if (isText)
            {
                MessageContent.text = Message.GetMessageBody();
            }
            else
            {
                MessageContent.text = string.Empty;
            }
        }

        private bool IsMine()
        {
            if (Message == null)
                return false;
            if (Message.Target == ChatTarget.SYSTEM)
                return false;
            if (Message.Sender == null)
                return false;
            return Message.Sender.ProfileID == Profile.ProfileID;
        }

        private void CancelEditMode()
        {
            IsEditMode = false;
            EditField.text = string.Empty;
            EditField.gameObject.SetActive(false);
            MessageContent.gameObject.SetActive(true);
            SaveButton.SetActive(false);
            EditButton.SetActive(true);
        }

        private void CancelBanMode()
        {
            IsBanMode = false;
            ConfirmBanButton.SetActive(false);
            BanButton.SetActive(true);
            BanPanel.SetActive(false);
        }

        // button click
        public void ClosePopup()
        {
            gameObject.SetActive(false);
        }

        public void ViewProfile()
        {
            if (Message == null || IsMine())
                return;
            var sender = Message.Sender;
            var profileID = sender.ProfileID;
            new PopupViewer().ShowUserInfo(profileID);
            ClosePopup();
        }

        public void SaveRequest()
        {
            var messageID = Message.MessageID;
            var chatID = Message.ChatID;
            var textToEdit = EditField.text;
            CBSChat.ChangeMessageText(messageID, chatID, textToEdit, onEdit =>
            {
                if (onEdit.IsSuccess)
                {
                    var newMessage = onEdit.ModifiedMessage;
                    Message = newMessage;
                    DisplayPopup();
                    ModifyAction?.Invoke(newMessage);
                }
                else
                {
                    new PopupViewer().ShowFabError(onEdit.Error);
                }
            });
        }

        public void EditRequest()
        {
            if (IsBanMode)
                CancelBanMode();
            IsEditMode = true;
            EditButton.SetActive(false);
            SaveButton.SetActive(true);
            MessageContent.gameObject.SetActive(false);
            EditField.gameObject.SetActive(true);
            EditField.text = Message.GetMessageBody();
        }

        public void DeleteRequest()
        {
            var messageID = Message.MessageID;
            var chatID = Message.ChatID;
            CBSChat.DeleteChatMessage(messageID, chatID, onDelete =>
            {
                if (onDelete.IsSuccess)
                {
                    var newMessage = onDelete.ModifiedMessage;
                    Message = newMessage;
                    ModifyAction?.Invoke(newMessage);
                    ClosePopup();
                }
                else
                {
                    new PopupViewer().ShowFabError(onDelete.Error);
                }
            });
        }

        public void BanRequest()
        {
            if (IsEditMode)
                CancelEditMode();
            IsBanMode = true;
            BanButton.SetActive(false);
            ConfirmBanButton.SetActive(true);
            MessageContent.gameObject.SetActive(false);
            BanPanel.SetActive(true);
            HoursField.text = ChatUtils.DefaultBanHours.ToString();
            ReasonField.text = ChatUtils.DefaultBanReason;
        }

        public void ConfirmBan()
        {
            var banHours = 0;
            int.TryParse(HoursField.text, out banHours);
            var reason = ReasonField.text;
            var chatID = Message.ChatID;
            var sender = Message.Sender;
            if (sender == null)
                return;
            var profileIDToBan = sender.ProfileID;
            CBSChat.BanProfile(profileIDToBan, chatID, reason, banHours, onBan =>
            {
                if (onBan.IsSuccess)
                {
                    ClosePopup();
                }
                else
                {
                    new PopupViewer().ShowFabError(onBan.Error);
                }
            });
        }
    }
}
