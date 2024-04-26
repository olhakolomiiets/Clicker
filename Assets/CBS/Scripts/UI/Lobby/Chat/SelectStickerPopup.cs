using CBS.Models;
using CBS.Scriptable;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class SelectStickerPopup : MonoBehaviour
    {
        [SerializeField]
        private StickerScroller Scroller;
        [SerializeField]
        private GameObject SendButton;
        [SerializeField]
        private ToggleGroup Group;

        private ICBSChat Chat { get; set; }
        private ChatPrefabs ChatPrefabs { get; set; }
        private Action<ChatSticker> SelectAction { get; set; }
        private ChatSticker SelectedSticker { get; set; }

        private void Awake()
        {
            Chat = CBSModule.Get<CBSChatModule>();
            ChatPrefabs = CBSScriptable.Get<ChatPrefabs>();
        }

        public void Show(Action<ChatSticker> selectAction)
        {
            SelectAction = selectAction;
            SelectedSticker = null;
            SendButton.SetActive(false);
            LoadStickers();
        }

        private void LoadStickers()
        {
            Chat.GetStickersPack(OnGetStickers);
        }

        private void DisplayStickers(List<ChatSticker> stickers)
        {
            var sticersCount = stickers.Count;
            Scroller.SetPoolCount(sticersCount);
            var stickerPrefab = ChatPrefabs.StickerSlot;
            var stickersUI = Scroller.Spawn(stickerPrefab, stickers);
            foreach (var stickerUI in stickersUI)
            {
                stickerUI.GetComponent<StickerDrawer>().Configure(OnSelectSticker, Group);
            }
        }

        private void OnGetStickers(CBSGetStickersResult result)
        {
            if (result.IsSuccess)
            {
                var stickers = result.Stickers;
                DisplayStickers(stickers);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnSelectSticker(ChatSticker sticker)
        {
            SelectedSticker = sticker;
            SendButton.SetActive(true);
        }

        public void SelectStickerHandler()
        {
            if (SelectedSticker != null)
            {
                SelectAction?.Invoke(SelectedSticker);
                gameObject.SetActive(false);
            }
        }
    }
}
