using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class BubbleChatMessage : MonoBehaviour, IScrollableItem<ChatMessage>, ITagAction
    {
        [SerializeField]
        private Text Body;
        [SerializeField]
        private Text Nickname;
        [SerializeField]
        private Text Date;
        [SerializeField]
        private Image Bubble;
        [SerializeField]
        private AvatarDrawer Avatar;
        [SerializeField]
        private Image SlotDrawer;

        private readonly float SlotSize = 100f;

        private ChatMessage Message { get; set; }
        private ChatLocalConfig ChatConfig { get; set; }

        private RectTransform TextRect { get; set; }
        private RectTransform BubbleRect { get; set; }
        private RectTransform RootRect { get; set; }
        private LongClickTrigger LongClick { get; set; }

        private Vector2 DefaultTextSize { get; set; }
        private Vector2 DefaultBubbleSize { get; set; }
        private Vector2 DefaultRootSize { get; set; }
        private bool IsSlotRequest { get; set; }
        public Action<ChatMember> TagAction { get; set; }

        private IProfile Profile { get; set; }

        private void Awake()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
            TextRect = Body.GetComponent<RectTransform>();
            BubbleRect = Bubble.GetComponent<RectTransform>();
            RootRect = GetComponent<RectTransform>();
            LongClick = GetComponentInChildren<LongClickTrigger>();
            ChatConfig = CBSScriptable.Get<ChatLocalConfig>();
            DefaultTextSize = TextRect.sizeDelta;
            DefaultBubbleSize = BubbleRect.sizeDelta;
            DefaultRootSize = RootRect.sizeDelta;

            LongClick.OnLongClick += OnLongClick;
        }

        private void OnDestroy()
        {
            LongClick.OnLongClick -= OnLongClick;
        }

        public void Display(ChatMessage message)
        {
            Message = message;
            var target = Message.Target;
            //reset size
            TextRect.sizeDelta = DefaultTextSize;
            BubbleRect.sizeDelta = DefaultBubbleSize;
            RootRect.sizeDelta = DefaultRootSize;
            var contentType = Message.ContentType;
            SlotDrawer.gameObject.SetActive(contentType != MessageContent.MESSAGE);
            IsSlotRequest = false;

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
            string body = Message.GetMessageBody();
            var bubbleColor = ChatConfig.SystemBubbleColor;
            var defaultTextColor = ChatConfig.BubbleTextColor;
            Body.color = defaultTextColor;
            Bubble.color = bubbleColor;
            body = ChatUtils.MarkAsItalic(body);

            Nickname.text = ChatUtils.SystemMessageTitle;
            Body.text = body;

            // draw date
            var date = Message.CreationDateUTC.ToLocalTime();
            Date.text = date.ToString("MM/dd/yyyy H:mm");

            // draw avatars
            Avatar.DisplayDefaultAvatar();
        }

        private void DrawDefaultMessage(ChatMessage message)
        {
            Body.text = string.Empty;
            var sender = message.Sender;
            var state = message.State;
            // bold nickname
            string nickname = sender.DisplayName;
            // check is mine
            if (sender.ProfileID == Profile.ProfileID)
            {
                var newColor = ChatConfig.OwnerBubbleColor;
                Bubble.color = newColor;
            }
            else
            {
                Bubble.color = Color.white;
            }
            // draw avatars
            var profileID = sender.ProfileID;
            var avatarInfo = sender.Avatar;
            Avatar.LoadProfileAvatar(profileID, avatarInfo);
            // draw nickname
            var profileColor = ChatColorFactory.GetProfileColor(sender.ProfileID);
            nickname = ChatUtils.AddColorTag(profileColor, nickname);
            Nickname.text = nickname;
            // draw date
            var drawDate = ChatConfig.ShowDate;
            if (drawDate)
            {
                var date = Message.CreationDateUTC.ToLocalTime();
                Date.text = date.ToString("MM/dd/yyyy H:mm");
            }
            else
            {
                Date.text = string.Empty;
            }
            // draw tag
            var hasTag = message.TaggedProfile != null;
            if (hasTag)
            {
                var tagProfileName = message.TaggedProfile.DisplayName;
                var tagNickname = ChatUtils.MarkNickName(tagProfileName);
                var tagColor = ChatColorFactory.GetTagColor();
                tagNickname = ChatUtils.AddColorTag(tagColor, tagNickname);
                Body.text = tagNickname;
            }

            if (state == MessageState.DELETED)
            {
                var deleteMessage = ChatUtils.DeletedMessageText;
                deleteMessage = ChatUtils.MarkAsItalic(deleteMessage);
                Body.text = deleteMessage;
            }
            else
            {
                var contentType = message.ContentType;
                if (contentType == MessageContent.MESSAGE)
                {
                    var body = message.GetMessageBody();
                    DrawTextMessage(body);
                }
                else if (contentType == MessageContent.STICKER)
                {
                    var sticker = message.GetSticker();
                    DrawSticker(sticker);
                }
                else if (contentType == MessageContent.ITEM)
                {
                    var item = message.GetItem();
                    DrawItem(item);
                }
            }
        }

        private void DrawTextMessage(string body)
        {
            Body.text = Body.text + body;
        }

        private void DrawSticker(ChatSticker sticker)
        {
            SlotDrawer.sprite = sticker.GetSprite();
        }

        private void DrawItem(CBSInventoryItem item)
        {
            SlotDrawer.sprite = item.GetSprite();
        }

        private void ResizeBody()
        {
            var contentType = Message.ContentType;
            if (contentType == MessageContent.MESSAGE)
            {
                float preferredHeight = Body.preferredHeight;
                float defaultHeight = DefaultTextSize.y;
                float height = preferredHeight > defaultHeight ? preferredHeight : defaultHeight;
                TextRect.sizeDelta = new Vector2(DefaultTextSize.x, height);

                float bubbleHeight = BubbleRect.sizeDelta.y;
                float upHeight = preferredHeight > defaultHeight ? preferredHeight - defaultHeight : 0;
                bubbleHeight += upHeight;
                BubbleRect.sizeDelta = new Vector2(DefaultBubbleSize.x, bubbleHeight);

                RootRect.sizeDelta = new Vector2(RootRect.sizeDelta.x, RootRect.sizeDelta.y + upHeight);
            }
            else
            {
                var bubbleWidth = SlotSize + 25;
                var bubbleHeight = SlotSize + 35;
                SlotDrawer.rectTransform.sizeDelta = new Vector2(SlotSize, SlotSize);
                BubbleRect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);
                RootRect.sizeDelta = new Vector2(RootRect.sizeDelta.x, bubbleHeight + 40);
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
            if (Message == null)
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
