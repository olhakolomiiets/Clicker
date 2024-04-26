using CBS.Models;
using CBS.Scriptable;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class SelectItemPopup : MonoBehaviour
    {
        [SerializeField]
        private SimpleItemScroller Scroller;
        [SerializeField]
        private GameObject SendButton;
        [SerializeField]
        private ToggleGroup Group;

        private ICBSInventory CBSInventory { get; set; }
        private ChatPrefabs ChatPrefabs { get; set; }
        private Action<CBSInventoryItem> SelectAction { get; set; }
        private CBSInventoryItem SelectedItem { get; set; }

        private void Awake()
        {
            CBSInventory = CBSModule.Get<CBSInventoryModule>();
            ChatPrefabs = CBSScriptable.Get<ChatPrefabs>();
        }

        public void Show(Action<CBSInventoryItem> selectAction)
        {
            SelectAction = selectAction;
            SelectedItem = null;
            SendButton.SetActive(false);
            LoadItems();
        }

        private void LoadItems()
        {
            CBSInventory.GetInventory(OnGetItems);
        }

        private void DisplayItems(List<CBSInventoryItem> items)
        {
            var itemsCount = items.Count;
            Scroller.SetPoolCount(itemsCount);
            var itemPrefab = ChatPrefabs.ItemSlot;
            var itemsUI = Scroller.Spawn(itemPrefab, items);
            foreach (var itemUI in itemsUI)
            {
                itemUI.GetComponent<SimpleItemDrawer>().Configure(OnSelectItem, Group);
            }
        }

        private void OnGetItems(CBSGetInventoryResult result)
        {
            if (result.IsSuccess)
            {
                var items = result.NonEquippedItems;
                DisplayItems(items);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnSelectItem(CBSInventoryItem item)
        {
            SelectedItem = item;
            SendButton.SetActive(true);
        }

        public void SelectItemHandler()
        {
            if (SelectedItem != null)
            {
                SelectAction?.Invoke(SelectedItem);
                gameObject.SetActive(false);
            }
        }
    }
}
