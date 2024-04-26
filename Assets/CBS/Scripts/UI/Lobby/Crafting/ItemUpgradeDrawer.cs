using CBS.Models;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ItemUpgradeDrawer : MonoBehaviour
    {
        [SerializeField]
        private GameObject DependencyContainer;
        [SerializeField]
        private Color CustomDataTitleColor;
        [SerializeField]
        private Color CustomDataValueColor;

        [SerializeField]
        private Text UpgradeTitle;
        [SerializeField]
        private Text UpgradeData;
        [SerializeField]
        private ItemDependencyDrawer DependencyDrawer;
        [SerializeField]
        private bool Resize = true;
        [SerializeField]
        private float ResizeBottomOfset;
        [SerializeField]
        private RectTransform[] TransformsToCalculateHeight;

        public Action<CraftStateContainer> OnGetDependency;

        private ICrafting Crafting { get; set; }
        private ICBSItems Items { get; set; }

        private void Awake()
        {
            Crafting = CBSModule.Get<CBSCraftingModule>();
            Items = CBSModule.Get<CBSItemsModule>();
        }

        public void Load(CBSInventoryItem item, bool fromCache = false)
        {
            DependencyContainer.SetActive(false);
            UpgradeTitle.text = ItemTXTHandler.GetUpgradeTitle(item.UpgradeIndex + 1, item.IsMaxUpgrade());
            UpgradeData.text = item.GetNextUpgradeCustomDataAsReadableText(CustomDataTitleColor, CustomDataValueColor);

            if (fromCache)
            {
                Crafting.GetItemNextUpgradeStateFromCache(item.InstanceID, OnGetDependencyState);
            }
            else
            {
                Crafting.GetItemNextUpgradeState(item.InstanceID, OnGetDependencyState);
            }
            if (Resize)
                CalculateSize();
        }

        private void DrawDependency(CraftStateContainer state)
        {
            if (state == null)
            {
                DependencyContainer.SetActive(false);
            }
            else
            {
                DependencyContainer.SetActive(!state.IsFreeToCraft());
                DependencyDrawer.Draw(state);
            }
            if (Resize)
                CalculateSize();
        }

        private void CalculateSize()
        {
            var height = UpgradeData.preferredHeight;
            var rectTr = UpgradeData.GetComponent<RectTransform>();
            var sizeDelta = rectTr.sizeDelta;
            sizeDelta.y = height;
            rectTr.sizeDelta = sizeDelta;

            var finalHeight = 0f;
            foreach (var rect in TransformsToCalculateHeight)
            {
                if (rect.gameObject.activeInHierarchy)
                    finalHeight += rect.sizeDelta.y;
            }
            var rootTransform = gameObject.GetComponent<RectTransform>();
            var rootSize = rootTransform.sizeDelta;
            rootSize.y = finalHeight + ResizeBottomOfset;
            rootTransform.sizeDelta = rootSize;
        }

        // events
        private void OnGetDependencyState(CBSGetNextUpgradeStateResult result)
        {
            if (result.IsSuccess)
            {
                var stateContainer = result.DependencyState;
                DrawDependency(stateContainer);
                OnGetDependency?.Invoke(stateContainer);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
