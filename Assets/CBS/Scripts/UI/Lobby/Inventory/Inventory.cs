using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class Inventory : MonoBehaviour
    {
        [SerializeField]
        private ToggleGroup CategoryGroup;
        [SerializeField]
        private BaseScroller CategoryScroller;
        [SerializeField]
        private BaseScroller ItemsScroller;

        private string[] CurrentCategories { get; set; }
        private List<CBSInventoryItem> CurrentItems { get; set; }
        private string CurrentCategory { get; set; }

        private ICBSItems Items { get; set; }
        private ICBSInventory CBSInventory { get; set; }
        private ICrafting Crafting { get; set; }

        private InventoryPrefabs InventoryPrefabs { get; set; }
        private Dictionary<string, InventorySlot> SlotCache;

        private void Awake()
        {
            CBSInventory = CBSModule.Get<CBSInventoryModule>();
            Items = CBSModule.Get<CBSItemsModule>();
            Crafting = CBSModule.Get<CBSCraftingModule>();
            InventoryPrefabs = CBSScriptable.Get<InventoryPrefabs>();
            SlotCache = new Dictionary<string, InventorySlot>();
            // add listeners
            CategoryScroller.OnSpawn += OnCategorySpawned;
            ItemsScroller.OnSpawn += OnItemSpawn;
        }

        private void OnDestroy()
        {
            CategoryScroller.OnSpawn -= OnCategorySpawned;
            ItemsScroller.OnSpawn -= OnItemSpawn;
        }

        private void OnEnable()
        {
            CBSInventory.OnItemUnEquiped += OnItemEquipmentChange;
            CBSInventory.OnItemEquiped += OnItemEquipmentChange;
            CBSInventory.OnItemUsageCountChange += OnItemConsumed;
            CBSInventory.OnItemRemoved += OnItemRemoved;
            Crafting.OnItemUpgraded += OnItemUpgraded;


            DisplayCategories();
        }

        private void OnDisable()
        {
            CBSInventory.OnItemUnEquiped -= OnItemEquipmentChange;
            CBSInventory.OnItemEquiped -= OnItemEquipmentChange;
            CBSInventory.OnItemUsageCountChange -= OnItemConsumed;
            CBSInventory.OnItemRemoved -= OnItemRemoved;
            Crafting.OnItemUpgraded -= OnItemUpgraded;
        }

        // category
        private void DisplayCategories()
        {
            CategoryGroup.SetAllTogglesOff();
            CurrentCategories = Items.ItemCategories;

            // add ALL tab
            var categoriesList = CurrentCategories.ToList();
            categoriesList.Insert(0, UIUtils.ALL_MENU_TITLE);
            CurrentCategories = categoriesList.ToArray();

            int count = CurrentCategories.Length;
            var categoryPrefab = InventoryPrefabs.CategoryTab;
            CategoryScroller.SpawnItems(categoryPrefab, count);
        }

        // draw items
        private void DrawItems()
        {
            int count = CurrentItems == null ? 0 : CurrentItems.Count;
            var slotPrefab = InventoryPrefabs.InventorySlot;
            ItemsScroller.SpawnItems(slotPrefab, count);
        }

        private void OnCategorySpawned(GameObject uiItem, int index)
        {
            var scroll = CategoryScroller.GetComponent<ScrollRect>();
            float contentWidth = scroll.GetComponent<RectTransform>().sizeDelta.x;
            int categoriesCount = CurrentCategories.Length;
            float tabWidth = contentWidth / categoriesCount;

            var rectComponent = uiItem.GetComponent<RectTransform>();
            rectComponent.sizeDelta = new Vector2(tabWidth, rectComponent.sizeDelta.y);
            var toggleComponent = uiItem.GetComponent<Toggle>();
            toggleComponent.group = CategoryGroup;
            toggleComponent.isOn = false;
            var tabComponent = uiItem.GetComponent<CategoryTab>();
            tabComponent.TabObject = CurrentCategories[index];
            tabComponent.SetSelectAction(OnCategorySelected);
            if (index == 0)
            {
                toggleComponent.isOn = true;
            }
        }

        private void OnCategorySelected(string category)
        {
            // fetch items
            CurrentCategory = category;
            ItemsScroller.Clear();
            if (category == UIUtils.ALL_MENU_TITLE)
                CBSInventory.GetInventory(OnGetInventory);
            else
                CBSInventory.GetInventoryByCategory(category, OnGetInventory);
        }

        private void OnGetInventory(CBSGetInventoryResult result)
        {
            if (result.IsSuccess)
            {
                CurrentItems = result.NonEquippedItems;
                DrawItems();
            }
        }

        private void OnItemSpawn(GameObject uiItem, int index)
        {
            var item = CurrentItems[index];
            var inventoryID = item.InstanceID;
            var uiComponent = uiItem.GetComponent<InventorySlot>();
            SlotCache[inventoryID] = uiComponent;
            uiComponent.Init(item, OnItemClicked);
        }

        // events
        private void OnItemClicked(CBSInventoryItem item)
        {
            var uiPrefab = InventoryPrefabs.ItemInfo;
            var uiObject = UIView.ShowWindow(uiPrefab);
            var itemInfo = uiObject.GetComponent<InventoryItemInfo>();
            itemInfo.Draw(item);
        }

        private void OnItemEquipmentChange(CBSInventoryItem item)
        {
            if (CurrentCategory == UIUtils.ALL_MENU_TITLE)
                CBSInventory.GetInventory(OnGetInventory);
            else
                CBSInventory.GetInventoryByCategory(CurrentCategory, OnGetInventory);
        }

        private void OnItemConsumed(ItemUsesCountChange changeResult)
        {
            var inventoryItemID = changeResult.ItemInventoryID;
            var countLeft = changeResult.UsesLeft;
            if (SlotCache.ContainsKey(inventoryItemID))
            {
                if (countLeft == 0)
                {
                    SlotCache[inventoryItemID].gameObject.SetActive(false);
                }
                else
                {
                    var cbsItem = changeResult.ChangedItem;
                    if (cbsItem != null)
                    {
                        SlotCache[inventoryItemID].UpdateItem(cbsItem);
                    }
                    SlotCache[inventoryItemID].UpdateCount((int)countLeft);
                }
            }
        }

        private void OnItemRemoved(string inventoryItemID)
        {
            if (SlotCache.ContainsKey(inventoryItemID))
            {
                SlotCache[inventoryItemID].gameObject.SetActive(false);
            }
        }

        private void OnItemUpgraded(CBSInventoryItem item)
        {
            var inventoryItemID = item.InstanceID;
            SlotCache[inventoryItemID].UpdateItem(item);
        }
    }
}
