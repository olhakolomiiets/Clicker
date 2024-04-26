using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class InventorySlot : MonoBehaviour
    {
        [SerializeField]
        private Image Icon;
        [SerializeField]
        private GameObject CounterBack;
        [SerializeField]
        private Text Counter;

        public CBSInventoryItem Item { get; private set; }
        private Action<CBSInventoryItem> ClickAction { get; set; }

        private ICBSInventory CBSInventory { get; set; }

        protected virtual void Awake()
        {
            CBSInventory = CBSModule.Get<CBSInventoryModule>();
        }

        public void Init(CBSInventoryItem item, Action<CBSInventoryItem> onClick)
        {
            Item = item;
            ClickAction = onClick;
            // draw icon
            Icon.sprite = Item.GetSprite();
            // draw count
            bool hasCount = item.Count != null && item.Count != 0;
            CounterBack.SetActive(hasCount);
            var count = hasCount ? item.Count : 0;
            UpdateCount((int)count);
        }

        public void UpdateCount(int count)
        {
            if (Item.IsConsumable)
            {
                CounterBack.GetComponent<Image>().color = Color.green;
            }
            else if (Item.IsStackable)
            {
                CounterBack.GetComponent<Image>().color = Color.red;
            }

            Counter.text = count.ToString();
        }

        public void UpdateItem(CBSInventoryItem item)
        {
            Item = item;
        }

        public void ClickSlot()
        {
            ClickAction?.Invoke(Item);
        }
    }
}
