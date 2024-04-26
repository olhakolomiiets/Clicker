using CBS.Core;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Toggle))]
    public class SimpleItemDrawer : MonoBehaviour, IScrollableItem<CBSInventoryItem>
    {
        [SerializeField]
        private Image Icon;

        private Toggle Toggle { get; set; }
        private CBSInventoryItem Item { get; set; }
        private Action<CBSInventoryItem> SelectAction { get; set; }

        private void Awake()
        {
            Toggle = GetComponent<Toggle>();
            Toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        private void OnDestroy()
        {
            Toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }

        public void Display(CBSInventoryItem item)
        {
            Item = item;
            Icon.sprite = item.GetSprite();
            Toggle.SetIsOnWithoutNotify(false);
        }

        public void Configure(Action<CBSInventoryItem> selectAction, ToggleGroup toggleGroup)
        {
            Toggle.group = toggleGroup;
            SelectAction = selectAction;
        }

        private void OnToggleValueChanged(bool val)
        {
            if (val && Item != null)
            {
                SelectAction?.Invoke(Item);
            }
        }
    }
}
