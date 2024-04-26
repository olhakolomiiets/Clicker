using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class Equipments : MonoBehaviour
    {
        [SerializeField]
        private BaseScroller ItemsScroller;

        private List<CBSInventoryItem> CurrentItems { get; set; }
        private ICBSInventory CBSInventory { get; set; }
        private InventoryPrefabs Prefabs { get; set; }

        private void Awake()
        {
            CBSInventory = CBSModule.Get<CBSInventoryModule>();
            Prefabs = CBSScriptable.Get<InventoryPrefabs>();
        }

        private void OnEnable()
        {
            ItemsScroller.OnSpawn += OnItemSpawn;
            CBSInventory.OnItemEquiped += OnItemEquipmentChange;
            CBSInventory.OnItemUnEquiped += OnItemEquipmentChange;
            DisplayEquipments();
        }

        private void OnDisable()
        {
            ItemsScroller.OnSpawn -= OnItemSpawn;
            CBSInventory.OnItemEquiped -= OnItemEquipmentChange;
            CBSInventory.OnItemUnEquiped -= OnItemEquipmentChange;
        }

        private void DisplayEquipments()
        {
            CBSInventory.GetInventory(OnGetInvertory);
        }

        private void OnGetInvertory(CBSGetInventoryResult result)
        {
            if (result.IsSuccess)
            {
                CurrentItems = result.EquippedItems;
                DrawItems();
            }
        }

        private void DrawItems()
        {
            int count = CurrentItems == null ? 0 : CurrentItems.Count;
            var slotPrefab = Prefabs.InventorySlot;
            ItemsScroller.SpawnItems(slotPrefab, count);
        }

        // events
        private void OnItemSpawn(GameObject uiItem, int index)
        {
            var item = CurrentItems[index];
            var uiComponent = uiItem.GetComponent<InventorySlot>();
            uiComponent.Init(item, OnItemClicked);
        }

        private void OnItemEquipmentChange(CBSInventoryItem item)
        {
            DisplayEquipments();
        }

        private void OnItemClicked(CBSInventoryItem item)
        {
            var uiPrefab = Prefabs.ItemInfo;
            var uiObject = UIView.ShowWindow(uiPrefab);
            var itemInfo = uiObject.GetComponent<InventoryItemInfo>();
            itemInfo.Draw(item);
        }
    }
}
