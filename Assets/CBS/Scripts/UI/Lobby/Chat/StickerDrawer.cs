using CBS.Core;
using CBS.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Toggle))]
    public class StickerDrawer : MonoBehaviour, IScrollableItem<ChatSticker>
    {
        [SerializeField]
        private Image Icon;

        private Toggle Toggle { get; set; }
        private ChatSticker Sticker { get; set; }
        private Action<ChatSticker> SelectAction { get; set; }

        private void Awake()
        {
            Toggle = GetComponent<Toggle>();
            Toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        private void OnDestroy()
        {
            Toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }

        public void Display(ChatSticker sticker)
        {
            Sticker = sticker;
            Icon.sprite = sticker.GetSprite();
            Toggle.SetIsOnWithoutNotify(false);
        }

        public void Configure(Action<ChatSticker> selectAction, ToggleGroup toggleGroup)
        {
            Toggle.group = toggleGroup;
            SelectAction = selectAction;
        }

        private void OnToggleValueChanged(bool val)
        {
            if (val && Sticker != null)
            {
                SelectAction?.Invoke(Sticker);
            }
        }
    }
}
