using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ChatView : MonoBehaviour, IDisposable
    {
        [SerializeField]
        private InputField MessageInput;
        [SerializeField]
        private ChatTagDrawer TagDrawer;
        [SerializeField]
        private ChatScroller Scroller;
        [SerializeField]
        private int MaxMessagesToLoad = 100;
        [SerializeField]
        private float UpateIntervalMiliseconds = 500f;
        [SerializeField]
        private ChatDisplayMode DisplayMode;
        [SerializeField]
        private float TextModeSpacing;
        [SerializeField]
        private float BubbleModeSpacing;

        private IChat Chat { get; set; }
        private IProfile Profile { get; set; }
        private bool IsInited { get; set; }
        private ChatPrefabs Prefabs { get; set; }
        private GameObject MessagePrefab { get; set; }

        private void Awake()
        {
            Prefabs = CBSScriptable.Get<ChatPrefabs>();
            Profile = CBSModule.Get<CBSProfileModule>();
            MessagePrefab = DisplayMode == ChatDisplayMode.TEXT_MODE ? Prefabs.SimpleChatMessage : Prefabs.BubbleChatMessage;
            ApplySpacing();
        }

        private void OnDisable()
        {
            Dispose();
        }

        public void Init(IChat chat)
        {
            if (IsInited)
                return;
            // init new chat
            IsInited = true;
            Chat = chat;
            Chat.OnNewMessage += OnNewMessageAdded;
            Chat.OnGetHistory += OnGetMessages;
            Chat.SetUpdateIntervalMiliseconds(UpateIntervalMiliseconds);
            Chat.SetMaxMessagesCount(MaxMessagesToLoad);
            Chat.Load();
            RemoveTag();
        }

        private void ApplySpacing()
        {
            var layoutComponent = Scroller.GetContentRoot().GetComponent<HorizontalOrVerticalLayoutGroup>();
            layoutComponent.spacing = DisplayMode == ChatDisplayMode.TEXT_MODE ? TextModeSpacing : BubbleModeSpacing;
        }

        public void SendMessage()
        {
            if (!IsInited)
                return;
            string messageBody = MessageInput.text;
            if (string.IsNullOrEmpty(messageBody))
                return;
            // clear message
            MessageInput.text = string.Empty;

            var messageRequest = new CBSSendTextMessageRequest
            {
                MessageBody = messageBody,
                TaggedProfileID = TagDrawer.TagProfileID
            };
            RemoveTag();
            Chat.SendMessage(messageRequest, onSent =>
            {
                if (!onSent.IsSuccess)
                {
                    new PopupViewer().ShowFabError(onSent.Error);
                }
            });
        }

        public void OpenItemUI()
        {
            new PopupViewer().ShowItemsPopup(item =>
            {
                var itemInventoryID = item.InstanceID;
                Chat.SendItem(new CBSSendItemMessageRequest
                {
                    InstanceID = itemInventoryID,
                    TaggedProfileID = TagDrawer.TagProfileID
                });
            });
        }

        public void OpenStickerUI()
        {
            new PopupViewer().ShowStickersPopup(sticker =>
            {
                var sticerID = sticker.ID;
                Chat.SendSticker(new CBSSendStickerMessageRequest
                {
                    StickerID = sticerID,
                    TaggedProfileID = TagDrawer.TagProfileID
                });
            });
        }

        public void RemoveTag()
        {
            TagDrawer.gameObject.SetActive(false);
        }

        public void TagProfile(ChatMember member)
        {
            var memberProfileID = member.ProfileID;
            if (Profile.ProfileID == memberProfileID)
                return;
            TagDrawer.gameObject.SetActive(true);
            TagDrawer.DrawTag(member);
        }

        // events
        public void OnNewMessageAdded(ChatMessage message)
        {
            Scroller.PushNewMessage(message);
        }

        public void OnGetMessages(List<ChatMessage> messages)
        {
            if (Chat == null)
                return;
            var uiList = Scroller.Spawn(MessagePrefab, messages);
            foreach (var ui in uiList)
            {
                ui.gameObject.GetComponent<ITagAction>().TagAction = TagProfile;
            }
            Scroller.SetScrollPosition(0);
        }

        public void Dispose()
        {
            Scroller.HideAll();
            IsInited = false;
            if (Chat != null)
            {
                Chat.OnNewMessage -= OnNewMessageAdded;
                Chat.OnGetHistory -= OnGetMessages;
            }
            Chat?.Dispose();
            Chat = null;
        }
    }
}
