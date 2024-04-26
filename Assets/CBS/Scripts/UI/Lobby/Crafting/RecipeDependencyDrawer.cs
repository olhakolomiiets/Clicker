using CBS.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class RecipeDependencyDrawer : MonoBehaviour
    {
        [SerializeField]
        private GameObject TargetContainer;
        [SerializeField]
        private GameObject DependencyContainer;

        [SerializeField]
        private Image TargetIcon;
        [SerializeField]
        private Text TargetTitle;
        [SerializeField]
        private Text TargetDescription;
        [SerializeField]
        private ItemDependencyDrawer DependencyDrawer;
        [SerializeField]
        private bool Resize = true;

        public Action<CraftStateContainer> OnGetDependency;

        private ICrafting Crafting { get; set; }
        private ICBSItems Items { get; set; }

        private void Awake()
        {
            Crafting = CBSModule.Get<CBSCraftingModule>();
            Items = CBSModule.Get<CBSItemsModule>();
        }

        public void Load(string recipeItemID, bool fromCache = false)
        {
            TargetContainer.SetActive(false);
            DependencyContainer.SetActive(false);

            if (fromCache)
            {
                Crafting.GetRecipeDependencyStateFromCache(recipeItemID, OnGetDependencyState);
            }
            else
            {
                Crafting.GetRecipeDependencyState(recipeItemID, OnGetDependencyState);
            }
        }

        private void DrawRecipe(string targetID, CraftStateContainer state)
        {
            TargetContainer.SetActive(true);
            DependencyContainer.SetActive(!state.IsFreeToCraft());

            var targetItem = Items.GetFromCache(targetID);
            TargetIcon.sprite = targetItem.GetSprite();
            TargetTitle.text = targetItem.DisplayName;
            TargetDescription.text = targetItem.Description;
            DependencyDrawer.Draw(state);
            if (Resize)
                CalculateSize();
        }

        private void CalculateSize()
        {
            var finalHeight = 0f;
            var targetRect = TargetContainer.GetComponent<RectTransform>();
            finalHeight += targetRect.sizeDelta.y;
            if (DependencyContainer.activeInHierarchy)
            {
                var dependencyRect = DependencyContainer.GetComponent<RectTransform>();
                finalHeight += dependencyRect.sizeDelta.y;
            }
            var thisRect = GetComponent<RectTransform>();
            var thisSize = thisRect.sizeDelta;
            thisSize.y = finalHeight;
            thisRect.sizeDelta = thisSize;
        }

        // events
        private void OnGetDependencyState(CBSGetRecipeDependencyStateResult result)
        {
            if (result.IsSuccess)
            {
                var stateContainer = result.DependencyState;
                var targetID = result.ItemIDToCraft;
                DrawRecipe(targetID, stateContainer);
                OnGetDependency?.Invoke(stateContainer);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
