using CBS.Scriptable;
using System;
using CBS.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class LootBoxSlot : MonoBehaviour
    {
        [SerializeField]
        private Image Icon;
        [SerializeField] 
        private GameObject Locker;
        [SerializeField] 
        private Text TimerLabel;

        private ItemsIcons ItemIcons { get; set; }
        private CBSLootboxInventoryItem Box { get; set; }

        private Action<CBSLootboxInventoryItem, LootBoxSlot> SelectAction { get; set; }

        private Toggle Toggle { get; set; }

        private void Awake()
        {
            ItemIcons = CBSScriptable.Get<ItemsIcons>();
            Toggle = gameObject.GetComponentInChildren<Toggle>();
            Toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        private void OnDestroy()
        {
            Toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }

        private void LateUpdate()
        {
            DisplayDate();
        }

        public void Configurate(CBSLootboxInventoryItem item, ToggleGroup group)
        {
            Box = item;
            // draw icon
            var sprite = ItemIcons.GetSprite(Box.ItemID);
            Icon.sprite = sprite;
            Toggle.group = group;
            // draw locker
            Locker.SetActive(Box.LockedByTimer);
        }

        public void SetSelectAction(Action<CBSLootboxInventoryItem, LootBoxSlot> action)
        {
            SelectAction = action;
        }
        
        private void DisplayDate()
        {
            Locker.SetActive(Box.LockedByTimer);
            if (!Box.LockedByTimer)
                return;
            var nextDate = Box.UnlockDate;
            if (nextDate == null)
                TimerLabel.text = string.Empty;
            else
                TimerLabel.text = LootboxUtils.GetNextDateNotification(nextDate.GetValueOrDefault());
        }

        private void OnToggleValueChanged(bool val)
        {
            if (val)
            {
                SelectAction?.Invoke(Box, this);
            }
        }

        public void SetToggleValue(bool val)
        {
            Toggle.isOn = val;
        }
    }
}
