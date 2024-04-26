using CBS.Models;
using CBS.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class CraftingWindow : MonoBehaviour
    {
        [SerializeField]
        private CraftDropArea DropArea;
        [SerializeField]
        private Transform RootDrop;
        [SerializeField]
        private GameObject DragAndDropNotification;
        [SerializeField]
        private RecipeDependencyDrawer DependencyDrawer;
        [SerializeField]
        private Button CraftButton;

        private ICrafting Crafting { get; set; }
        private DraggableInventorySlot DroppedSlot { get; set; }

        private void Start()
        {
            Crafting = CBSModule.Get<CBSCraftingModule>();
            DropArea.OnDropSlot += OnDropInventoryItem;
            DependencyDrawer.OnGetDependency += OnGetRecipeDependency;
        }

        private void OnDestroy()
        {
            DropArea.OnDropSlot -= OnDropInventoryItem;
            DependencyDrawer.OnGetDependency -= OnGetRecipeDependency;
        }

        private void OnEnable()
        {
            UnloadRecipe();
        }

        private void OnDisable()
        {
            if (DroppedSlot != null)
            {
                DroppedSlot.OnStartDrag -= OnCancelCraft;
                DroppedSlot.ReturnToInitialPosition();
                UnloadRecipe();
            }
        }

        private void LoadRecipe(CBSInventoryItem item)
        {
            var recipeID = item.ItemID;
            DependencyDrawer.gameObject.SetActive(true);
            DragAndDropNotification.SetActive(false);
            DependencyDrawer.Load(recipeID);
        }

        private void UnloadRecipe()
        {
            DragAndDropNotification.SetActive(true);
            DependencyDrawer.gameObject.SetActive(false);
            CraftButton.interactable = false;
            DroppedSlot = null;
        }

        // events
        private void OnDropInventoryItem(DraggableInventorySlot slot)
        {
            if (DroppedSlot != null)
            {
                DroppedSlot.OnStartDrag -= OnCancelCraft;
                DroppedSlot.ReturnToInitialPosition();
                UnloadRecipe();
            }
            DroppedSlot = slot;
            var instance = slot.Item;
            DroppedSlot.SetParent(RootDrop);
            LoadRecipe(instance);
            DroppedSlot.OnStartDrag += OnCancelCraft;
        }

        private void OnCancelCraft()
        {
            if (DroppedSlot != null)
            {
                DroppedSlot.OnStartDrag -= OnCancelCraft;
            }
            UnloadRecipe();
        }

        private void OnGetRecipeDependency(CraftStateContainer state)
        {
            var isValid = state.ReadyToGraft();
            CraftButton.interactable = isValid;
        }

        // button click
        public void CraftItem()
        {
            if (DroppedSlot == null)
                return;
            var instance = DroppedSlot.Item;
            var instanceID = instance.InstanceID;

            Crafting.CraftItemFromRecipe(instanceID, onCraft =>
            {
                if (onCraft.IsSuccess)
                {
                    if (DroppedSlot != null)
                    {
                        DroppedSlot.OnStartDrag -= OnCancelCraft;
                        DroppedSlot.ReturnToInitialPosition();
                        DroppedSlot.gameObject.SetActive(false);
                        UnloadRecipe();
                    }
                    var craftedItem = onCraft.CraftedItemInstance;
                    new PopupViewer().ShowItemPopup(craftedItem.ItemID, ItemTXTHandler.CraftItemTitle);
                }
                else
                {
                    new PopupViewer().ShowFabError(onCraft.Error);
                }
            });
        }
    }
}
