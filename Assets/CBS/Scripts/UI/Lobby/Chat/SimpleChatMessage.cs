using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class SimpleChatMessage : MonoBehaviour, IScrollableItem<ChatMessage>, ITagAction
    {
        [SerializeField]
        private Text Body;
        [SerializeField]
        private RectTransform ClickableArea;
        [SerializeField]
        private Image SlotDrawer;

        private readonly float StickerSize = 50f;
        private readonly float ItemSize = 100f;

        private ChatMessage Message { get; set; }
        private RectTransform RectTransform { get; set; }
        private LongClickTrigger LongClick { get; set; }
        private Vector2 DefaultSize { get; set; }
        private IProfile Profile { get; set; }
        private ChatLocalConfig ChatConfig { get; set; }
        private RectTransform Parent { get; set; }
        private Rect ParentSize { get; set; }
        private bool IsSlotRequest { get; set; }
        public Action<ChatMember> TagAction { get; set; }

        private void Awake()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
            ChatConfig = CBSScriptable.Get<ChatLocalConfig>();
            RectTransform = GetComponent<RectTransform>();
            Parent = transform.parent.GetComponent<RectTransform>();
            LongClick = GetComponentInChildren<LongClickTrigger>();
            DefaultSize = RectTransform.sizeDelta;
            ParentSize = Parent.rect;

            LongClick.OnLongClick += OnLongClick;
        }

        private void OnDestroy()
        {
            LongClick.OnLongClick -= OnLongClick;
        }

        public void Display(ChatMessage message)
        {
            Message = message;
            var contentType = Message.ContentType;
            SlotDrawer.gameObject.SetActive(contentType != MessageContent.MESSAGE);
            var target = Message.Target;
            if (target == ChatTarget.DEFAULT)
            {
                DrawDefaultMessage(Message);
            }
            else if (target == ChatTarget.SYSTEM)
            {
                DrawSystemMessage(Message);
            }

            ResizeBody();
        }

        private void DrawSystemMessage(ChatMessage message)
        {
            string nickname = ChatUtils.MarkNickName(ChatUtils.SystemMessageTitle);
            // resize clickable area
            Body.text = nickname;
            var nickNameWidth = Body.preferredWidth;
            var clickableAreaSize = ClickableArea.sizeDelta;
            clickableAreaSize.x = nickNameWidth;
            ClickableArea.sizeDelta = clickableAreaSize;

            var displayDate = ChatConfig.ShowDate;
            if (displayDate)
            {
                var date = ChatUtils.MarkDate(message.CreationDateUTC);
                nickname = nickname + date;
            }

            string body = message.GetMessageBody();
            body = ChatUtils.MarkAsItalic(body);
            string full = nickname + " " + body;
            var systemColor = ChatConfig.SystemTextColor;
            string fullColored = ChatUtils.AddColorTag(systemColor, full);
            // finaly apply text
            Body.text = fullColored;
        }

        private void DrawDefaultMessage(ChatMessage message)
        {
            var sender = message.Sender;
            var contentType = message.ContentType;
            // bold nickname
            string nickname = ChatUtils.MarkNickName(sender.DisplayName);
            // resize clickable area
            Body.text = nickname;
            var nickNameWidth = Body.preferredWidth;
            var clickableAreaSize = ClickableArea.sizeDelta;
            clickableAreaSize.x = nickNameWidth;
            ClickableArea.sizeDelta = clickableAreaSize;

            var displayDate = ChatConfig.ShowDate;
            if (displayDate)
            {
                var date = ChatUtils.MarkDate(message.CreationDateUTC);
                nickname = nickname + date;
            }

            var hasTag = message.TaggedProfile != null;
            if (hasTag)
            {
                var tagProfileName = message.TaggedProfile.DisplayName;
                var tagNickname = ChatUtils.MarkNickName(tagProfileName);
                var tagColor = ChatColorFactory.GetTagColor();
                tagNickname = ChatUtils.AddColorTag(tagColor, tagNickname);
                nickname = nickname + tagNickname;
            }

            var state = message.State;
            if (state == MessageState.DELETED)
            {
                var profileColor = ChatColorFactory.GetProfileColor(sender.ProfileID);
                var deleteMessage = ChatUtils.DeletedMessageText;
                deleteMessage = ChatUtils.MarkAsItalic(deleteMessage);
                string full = nickname + " " + deleteMessage;
                full = ChatUtils.AddColorTag(profileColor, full);
                Body.text = full;
            }
            else
            {
                var senderID = sender.ProfileID;
                if (contentType == MessageContent.MESSAGE)
                {
                    string body = message.GetMessageBody();
                    DrawTextMessage(nickname, body, senderID);
                }
                else if (contentType == MessageContent.STICKER)
                {
                    var sticker = message.GetSticker();
                    DrawStickerMessage(nickname, sticker, senderID);
                }
                else if (contentType == MessageContent.ITEM)
                {
                    var item = message.GetItem();
                    DrawItemMessage(nickname, item, senderID);
                }
            }
        }

        private void DrawTextMessage(string nickname, string body, string senderProfileID)
        {
            string full = nickname + " " + body;
            var profileColor = ChatColorFactory.GetProfileColor(senderProfileID);
            string fullColored = ChatUtils.AddColorTag(profileColor, full);
            // finaly apply text
            Body.text = fullColored;
        }

        private void DrawStickerMessage(string nickname, ChatSticker sticker, string senderProfileID)
        {
            string full = nickname + " ";
            var profileColor = ChatColorFactory.GetProfileColor(senderProfileID);
            string fullColored = ChatUtils.AddColorTag(profileColor, full);
            // finaly apply text
            Body.text = fullColored;
            SlotDrawer.sprite = sticker.GetSprite();
        }

        private void DrawItemMessage(string nickname, CBSInventoryItem item, string senderProfileID)
        {
            string full = nickname + " ";
            var profileColor = ChatColorFactory.GetProfileColor(senderProfileID);
            string fullColored = ChatUtils.AddColorTag(profileColor, full);
            // finaly apply text
            Body.text = fullColored;
            SlotDrawer.sprite = item.GetSprite();
        }

        private void ResizeBody()
        {
            var contentType = Message.ContentType;
            float preferredHeight = Body.preferredHeight;
            float preferredWidth = Body.preferredWidth;
            if (contentType != MessageContent.MESSAGE)
            {
                var drawerPosition = SlotDrawer.rectTransform.anchoredPosition;
                drawerPosition.x = preferredWidth;
                SlotDrawer.rectTransform.anchoredPosition = drawerPosition;

                ResizeSlot(contentType);

                float height = SlotDrawer.rectTransform.sizeDelta.y;
                RectTransform.sizeDelta = new Vector2(ParentSize.width, height);
            }
            else
            {
                float defaultHeight = DefaultSize.y;
                float height = preferredHeight > defaultHeight ? preferredHeight : defaultHeight;
                RectTransform.sizeDelta = new Vector2(ParentSize.width, height);
            }
        }

        private void ResizeSlot(MessageContent content)
        {
            if (content == MessageContent.ITEM)
            {
                SlotDrawer.rectTransform.sizeDelta = new Vector2(ItemSize, ItemSize);
            }
            else if (content == MessageContent.STICKER)
            {
                SlotDrawer.rectTransform.sizeDelta = new Vector2(StickerSize, StickerSize);
            }
        }

        private void ClaimItemFromMessage()
        {
            if (IsSlotRequest)
                return;
            var messageID = Message.MessageID;
            var chatID = Message.ChatID;
            IsSlotRequest = true;
            CBSModule.Get<CBSChatModule>().ClaimItemFromChat(chatID, messageID, onClaim =>
            {
                IsSlotRequest = false;
                if (onClaim.IsSuccess)
                {
                    var item = onClaim.GrantedItem;
                    var itemID = item.ItemID;
                    new PopupViewer().ShowItemPopup(itemID, ChatUtils.ClaimItemTitle);
                }
                else
                {
                    new PopupViewer().ShowFabError(onClaim.Error);
                }
            });
        }

        // button events
        public void ClickNickName()
        {
            var target = Message.Target;
            if (target == ChatTarget.SYSTEM)
                return;
            var sender = Message.Sender;
            if (sender == null)
                return;
            TagAction?.Invoke(sender);
        }

        public void OnClickSlot()
        {
            var messageContent = Message.ContentType;
            if (messageContent == MessageContent.ITEM)
            {
                ClaimItemFromMessage();
            }
        }

        private void OnLongClick()
        {
            if (Message == null || Message.State == MessageState.DELETED)
                return;
            new PopupViewer().ShowMessagePopup(Message, OnModifyMessage);
        }

        // events
        private void OnModifyMessage(ChatMessage message)
        {
            Display(message);
        }
    }
}
