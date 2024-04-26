
using CBS.Models;
using System;

namespace CBS
{
    public interface ICrafting
    {
        /// <summary>
        /// Notify when item was crafted
        /// </summary>
        event Action<CBSInventoryItem> OnItemCrafted;

        /// <summary>
        /// Notify when item was upgraded
        /// </summary>
        event Action<CBSInventoryItem> OnItemUpgraded;

        /// <summary>
        /// Get the status of all ingredients for a craft item.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void GetRecipeDependencyState(string itemID, Action<CBSGetRecipeDependencyStateResult> result);

        /// <summary>
        /// Get the status of all ingredients for a craft item from cache. Requires "Preload Inventory", "Preload Currency" options enabled.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void GetRecipeDependencyStateFromCache(string itemID, Action<CBSGetRecipeDependencyStateResult> result);

        /// <summary>
        /// Craft item by inventory id (Instance ID) of the recipe. Requires instance of recipe in inventory.
        /// </summary>
        /// <param name="recipeInventoryID"></param>
        /// <param name="result"></param>
        void CraftItemFromRecipe(string recipeInventoryID, Action<CBSCraftResult> result);

        /// <summary>
        /// Craft item by item id (Catalog Item ID) of the recipe. Does not require an instance of recipe in inventory.
        /// </summary>
        /// <param name="recipeID"></param>
        /// <param name="result"></param>
        void CraftItemFromRecipeTemplate(string recipeID, Action<CBSCraftResult> result);

        /// <summary>
        /// Get the status of next item upgrade, include ingredients dependency
        /// </summary>
        /// <param name="inventoryItemID"></param>
        /// <param name="result"></param>
        void GetItemNextUpgradeState(string inventoryItemID, Action<CBSGetNextUpgradeStateResult> result);

        /// <summary>
        /// Get the status of next item upgrade from cache, include ingredients dependency. Requires "Preload Inventory", "Preload Currency" options enabled.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        void GetItemNextUpgradeStateFromCache(string inventoryItemID, Action<CBSGetNextUpgradeStateResult> result);

        /// <summary>
        /// Upgrade item to next level
        /// </summary>
        /// <param name="inventoryItemID"></param>
        /// <param name="result"></param>
        void UpgradeItemToNextLevel(string inventoryItemID, Action<CBSUpgradeItemResult> result);
    }
}
