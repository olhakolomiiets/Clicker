using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class ProfileInventoryInsideClan : MonoBehaviour
    {
        [SerializeField]
        private SimpleItemScroller Scroller;

        private ICBSInventory CBSInventory { get; set; }
        private IClanEconomy ClanEconomy { get; set; }
        private InventoryPrefabs Prefabs { get; set; }
        private Action<CBSInventoryItem> SelectAction { get; set; }

        public Action UpdateRequest;

        private void Awake()
        {
            CBSInventory = CBSModule.Get<CBSInventoryModule>();
            ClanEconomy = CBSModule.Get<CBSClanModule>();
            Prefabs = CBSScriptable.Get<InventoryPrefabs>();
        }

        public void Show(Action<CBSInventoryItem> selectAction)
        {
            SelectAction = selectAction;
            LoadInventory();
        }

        public void LoadInventory()
        {
            CBSInventory.GetInventory(OnGetItems);
        }

        private void DisplayItems(List<CBSInventoryItem> items)
        {
            Scroller.HideAll();
            var itemsCount = items.Count;
            var itemPrefab = Prefabs.InventorySlot;
            var itemsUI = Scroller.Spawn(itemPrefab, items);
            for (int i = 0; i < itemsUI.Count; i++)
            {
                itemsUI[i].GetComponent<InventorySlot>().Init(items[i], OnSelectItem);
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
            new PopupViewer().ShowYesNoPopup(new YesOrNoPopupRequest
            {
                Title = ClanTXTHandler.TransferTitle,
                Body = ClanTXTHandler.TransferItemToClan,
                OnYesAction = () =>
                {
                    ClanEconomy.TransferItemFromProfileToClan(item.InstanceID, onTransfer =>
                    {
                        if (onTransfer.IsSuccess)
                        {
                            UpdateRequest?.Invoke();
                        }
                        else
                        {
                            new PopupViewer().ShowFabError(onTransfer.Error);
                        }
                    });
                }
            });
        }
    }
}
