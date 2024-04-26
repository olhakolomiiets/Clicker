using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class ClanInventoryDrawer : MonoBehaviour
    {
        [SerializeField]
        private SimpleItemScroller Scroller;

        private IClanEconomy ClanEconomy { get; set; }
        private IProfile Profile { get; set; }
        private InventoryPrefabs Prefabs { get; set; }
        private Action<CBSInventoryItem> SelectAction { get; set; }

        public Action UpdateRequest;

        private void Awake()
        {
            ClanEconomy = CBSModule.Get<CBSClanModule>();
            Profile = CBSModule.Get<CBSProfileModule>();
            Prefabs = CBSScriptable.Get<InventoryPrefabs>();
        }

        public void Show(Action<CBSInventoryItem> selectAction)
        {
            SelectAction = selectAction;
            LoadInventory();
        }

        public void LoadInventory()
        {
            ClanEconomy.GetClanInventory(Profile.ClanID, OnGetItems);
        }

        private void DisplayItems(List<CBSInventoryItem> items)
        {
            Scroller.HideAll();
            var itemsCount = items.Count;
            var itemPrefab = Prefabs.InventorySlot;
            var itemsUI = Scroller.Spawn(itemPrefab, items);
            for (int i = 0; i < items.Count; i++)
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
                Body = ClanTXTHandler.TransferItemToProfile,
                OnYesAction = () =>
                {
                    ClanEconomy.TransferItemFromClanToProfile(item.InstanceID, onTransfer =>
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
