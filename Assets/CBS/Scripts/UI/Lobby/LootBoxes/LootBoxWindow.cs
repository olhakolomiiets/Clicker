using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class LootBoxWindow : MonoBehaviour
    {
        [SerializeField]
        private GameObject OpenBtn;
        [SerializeField]
        private GameObject UnlockBtn;
        [SerializeField]
        private BaseScroller Scroller;
        [SerializeField]
        private ToggleGroup Group;

        private ICBSInventory CBSInventory { get; set; }

        private List<CBSLootboxInventoryItem> CurrentBoxes { get; set; }

        private LootboxPrefabs Prefabs { get; set; }

        private CBSLootboxInventoryItem SelectedBox { get; set; }
        private LootBoxSlot SelectedSlot { get; set; }

        private void Awake()
        {
            CBSInventory = CBSModule.Get<CBSInventoryModule>();
            Prefabs = CBSScriptable.Get<LootboxPrefabs>();
            Scroller.OnSpawn += OnItemSpawn;
        }

        private void OnDestroy()
        {
            Scroller.OnSpawn -= OnItemSpawn;
        }

        private void OnEnable()
        {
            Scroller.Clear();
            GetLootBoxes();
        }

        private void GetLootBoxes()
        {
            OpenBtn.SetActive(false);
            UnlockBtn.SetActive(false);
            CBSInventory.GetLootboxes(OnLootBoxGetted);
        }

        private void OnLootBoxGetted(CBSGetLootboxesResult result)
        {
            if (result.IsSuccess)
            {
                Group.SetAllTogglesOff();
                var slotPrefab = Prefabs.LootBoxSlot;
                CurrentBoxes = result.Lootboxes;
                int count = CurrentBoxes == null ? 0 : CurrentBoxes.Count;
                Scroller.SpawnItems(slotPrefab, count);
            }
        }

        private void OnItemSpawn(GameObject uiItem, int index)
        {
            var box = CurrentBoxes[index];
            var slot = uiItem.GetComponent<LootBoxSlot>();
            slot.Configurate(box, Group);
            slot.SetSelectAction(OnBoxSelected);
            if (index == 0)
            {
                slot.SetToggleValue(true);
            }
        }

        private void OnBoxSelected(CBSLootboxInventoryItem box, LootBoxSlot slot)
        {
            SelectedSlot = slot;
            SelectedBox = box;
            OpenBtn.SetActive(SelectedBox != null && !SelectedBox.LockedByTimer);
            UnlockBtn.SetActive(SelectedBox != null && SelectedBox.LockedByTimer);
        }

        public void OpenLootBox()
        {
            if (SelectedBox == null)
                return;
            CBSInventory.OpenLootbox(SelectedBox.ItemID, SelectedBox.InstanceID, OnOpenLootBox);
        }
        
        public void UnlockLootBox()
        {
            if (SelectedBox == null)
                return;
            CBSInventory.UnlockLootboxTimer(SelectedBox.InstanceID, LootboxUnlocked);
        }

        private void OnOpenLootBox(CBSOpenLootboxResult result)
        {
            if (result.IsSuccess)
            {
                GetLootBoxes();
                var resultPrefab = Prefabs.LootBoxResult;
                var resultObject = UIView.ShowWindow(resultPrefab);
                resultObject.GetComponent<LootBoxResult>().Display(result);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
        
        private void LootboxUnlocked(CBSUnlockLootboxTimerResult result)
        {
            if (result.IsSuccess)
            {
                var updatedInstance = result.UpdatedInstance;
                SelectedSlot.Configurate(updatedInstance, Group);
                OnBoxSelected(updatedInstance, SelectedSlot);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
