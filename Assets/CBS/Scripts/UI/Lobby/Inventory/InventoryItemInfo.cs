using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class InventoryItemInfo : MonoBehaviour
    {
        [SerializeField]
        private Text Name;
        [SerializeField]
        private Text Description;
        [SerializeField]
        private Text CustomData;
        [SerializeField]
        private Image Icon;

        [SerializeField]
        private GameObject EquipButton;
        [SerializeField]
        private GameObject UnEquipButton;
        [SerializeField]
        private GameObject ConsumeButton;
        [SerializeField]
        private GameObject ThrowButton;
        [SerializeField]
        private GameObject UpgradeButton;
        [SerializeField]
        private GameObject CountContainer;
        [SerializeField]
        private GameObject UpgradeLevelContainer;
        [SerializeField]
        private GameObject RecipeContainer;
        [SerializeField]
        private GameObject UpgradeContainer;

        [SerializeField]
        private Color CustomDataTitleColor;
        [SerializeField]
        private Color CustomDataValueColor;

        private CBSInventoryItem Item { get; set; }

        private ICBSInventory CBSInventory { get; set; }

        private ICrafting CBSCrafting { get; set; }

        private ItemUpgradeDrawer UpgradeDrawer { get; set; }

        private void Awake()
        {
            CBSInventory = CBSModule.Get<CBSInventoryModule>();
            CBSCrafting = CBSModule.Get<CBSCraftingModule>();
            UpgradeDrawer = UpgradeContainer.GetComponent<ItemUpgradeDrawer>();
            UpgradeDrawer.OnGetDependency += OnGetUpgadeDependency;
        }

        private void OnDestroy()
        {
            UpgradeDrawer.OnGetDependency -= OnGetUpgadeDependency;
        }

        public void Draw(CBSInventoryItem item)
        {
            Item = item;
            DisplayFields();
            DisplayCustomData();
        }

        private void DisplayFields()
        {
            if (Item == null)
                return;
            EquipButton.SetActive(Item.IsEquippable && !Item.Equipped);
            UnEquipButton.SetActive(Item.Equipped);
            ConsumeButton.SetActive(Item.IsConsumable);
            CountContainer.SetActive(Item.IsConsumable);
            UpgradeButton.SetActive(Item.IsUpgradable && !Item.IsMaxUpgrade());
            RecipeContainer.SetActive(Item.IsRecipe);
            UpgradeLevelContainer.SetActive(Item.IsUpgradable);
            UpgradeContainer.SetActive(Item.IsUpgradable);
            UpgradeButton.GetComponent<Button>().interactable = false;
            if (Item.IsConsumable)
                CountContainer.GetComponentInChildren<Text>().text = Item.Count.ToString();
            if (Item.IsUpgradable)
                DisplayUpgrades();
            if (Item.IsRecipe)
                DisplayRecipe();

            Name.text = Item.DisplayName;
            Description.text = Item.Description;
            Icon.sprite = Item.GetSprite();
        }

        private void DisplayCustomData()
        {
            if (Item.IsUpgradable)
            {
                CustomData.text = Item.GetCurrentUpgradeCustomDataAsReadableText(CustomDataTitleColor, CustomDataValueColor);
            }
            else
            {
                CustomData.text = Item.GetCustomDataAsReadableText(CustomDataTitleColor, CustomDataValueColor);
            }

            var height = CustomData.preferredHeight;
            var rectTr = CustomData.GetComponent<RectTransform>();
            var sizeDelta = rectTr.sizeDelta;
            sizeDelta.y = height;
            rectTr.sizeDelta = sizeDelta;
        }

        private void DisplayUpgrades()
        {
            var authData = CBSScriptable.Get<AuthData>();
            var loadFromCache = authData.PreloadCurrency && authData.PreloadInventory;
            UpgradeLevelContainer.GetComponentInChildren<Text>().text = Item.UpgradeIndex.ToString();
            UpgradeContainer.GetComponent<ItemUpgradeDrawer>().Load(Item, loadFromCache);
        }

        private void DisplayRecipe()
        {
            var authData = CBSScriptable.Get<AuthData>();
            var loadFromCache = authData.PreloadCurrency && authData.PreloadInventory;
            RecipeContainer.GetComponent<RecipeDependencyDrawer>().Load(Item.ItemID, loadFromCache);
        }

        // events
        private void OnGetUpgadeDependency(CraftStateContainer craftState)
        {
            if (!Item.IsUpgradable)
                return;
            if (Item.IsMaxUpgrade())
                return;
            UpgradeButton.GetComponent<Button>().interactable = craftState.ReadyToGraft();
        }

        // button click
        public void ClickEquip()
        {
            CBSInventory.EquipItem(Item.InstanceID, result =>
            {
                ClickClose();
                if (!result.IsSuccess)
                {
                    new PopupViewer().ShowFabError(result.Error);
                }
            });
        }

        public void ClickUnequip()
        {
            CBSInventory.UnEquipItem(Item.InstanceID, result =>
            {
                ClickClose();
                if (!result.IsSuccess)
                {
                    new PopupViewer().ShowFabError(result.Error);
                }
            });
        }

        public void ClickConsume()
        {
            CBSInventory.ConsumeItem(Item.InstanceID, result =>
            {
                ClickClose();
                if (!result.IsSuccess)
                {
                    new PopupViewer().ShowFabError(result.Error);
                }
            });
        }

        public void ClickThowOut()
        {
            CBSInventory.RemoveInventoryItem(Item.InstanceID, onRemove =>
            {
                ClickClose();
                if (!onRemove.IsSuccess)
                {
                    new PopupViewer().ShowFabError(onRemove.Error);
                }
            });
        }

        public void ClickUpgrade()
        {
            CBSCrafting.UpgradeItemToNextLevel(Item.InstanceID, onUpgrade =>
            {
                if (onUpgrade.IsSuccess)
                {
                    var upgradedItem = onUpgrade.UpgradedItem;
                    Draw(upgradedItem);
                    UpgradeDrawer.Load(upgradedItem);
                }
                else
                {
                    new PopupViewer().ShowFabError(onUpgrade.Error);
                }
            });
        }

        public void ClickClose()
        {
            gameObject.SetActive(false);
        }
    }
}
