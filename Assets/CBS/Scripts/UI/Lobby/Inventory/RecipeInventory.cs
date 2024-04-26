using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class RecipeInventory : MonoBehaviour
    {
        [SerializeField]
        private BaseScroller ItemsScroller;

        private List<CBSInventoryItem> CurrentItems { get; set; }
        private ICBSInventory CBSInventory { get; set; }
        private CraftPrefabs CraftPrefabs { get; set; }
        private Dictionary<string, DraggableInventorySlot> SlotCache;

        private void Awake()
        {
            CBSInventory = CBSModule.Get<CBSInventoryModule>();
            CraftPrefabs = CBSScriptable.Get<CraftPrefabs>();
            SlotCache = new Dictionary<string, DraggableInventorySlot>();
            // add listeners
            ItemsScroller.OnSpawn += OnItemSpawn;
        }

        private void OnEnable()
        {
            CBSInventory.GetInventory(OnGetInventory);
        }

        private void OnDestroy()
        {
            ItemsScroller.OnSpawn -= OnItemSpawn;
        }

        // draw items
        private void DrawItems()
        {
            int count = CurrentItems == null ? 0 : CurrentItems.Count;
            var slotPrefab = CraftPrefabs.RecipeSlot;
            ItemsScroller.SpawnItems(slotPrefab, count);
        }

        private void OnGetInventory(CBSGetInventoryResult result)
        {
            if (result.IsSuccess)
            {
                CurrentItems = result.RecipeItems;
                DrawItems();
            }
        }

        private void OnItemSpawn(GameObject uiItem, int index)
        {
            var item = CurrentItems[index];
            var inventoryID = item.InstanceID;
            var uiComponent = uiItem.GetComponent<DraggableInventorySlot>();
            SlotCache[inventoryID] = uiComponent;
            uiComponent.Init(item, OnItemClicked);
        }

        // events
        private void OnItemClicked(CBSInventoryItem item)
        {

        }
    }
}
