using CBS.Models;
using System;

namespace CBS
{
    public interface ICBSInventory
    {
        /// <summary>
        /// Notify when uses count changed for item.
        /// </summary>
        event Action<ItemUsesCountChange> OnItemUsageCountChange;
        /// <summary>
        /// Notify when item was equipped.
        /// </summary>
        event Action<CBSInventoryItem> OnItemEquiped;
        /// <summary>
        /// Notify when item was unequipped.
        /// </summary>
        event Action<CBSInventoryItem> OnItemUnEquiped;
        /// <summary>
        /// Notify when item was added to inventory.
        /// </summary>
        event Action<CBSInventoryItem> OnItemAdded;
        /// <summary>
        /// Notify when item was removed from inventory.
        /// </summary>
        event Action<string> OnItemRemoved;
        /// <summary>
        /// Notify when loot box was added to inventory.
        /// </summary>
        event Action<CBSInventoryItem> OnLootboxAdded;
        /// <summary>
        /// Notifies when a user has opened a lootbox
        /// </summary>
        event Action<LootboxBundle> OnLootboxOpen;

        /// <summary>
        /// Get inventory items list of current profile.
        /// </summary>
        /// <param name="OnGetResult"></param>
        void GetInventory(Action<CBSGetInventoryResult> OnGetResult);

        /// <summary>
        /// Get inventory items list of current profile by specific category.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="OnGetResult"></param>
        void GetInventoryByCategory(string category, Action<CBSGetInventoryResult> OnGetResult);

        /// <summary>
        /// Get inventory items list by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void GetProfileInventoryByProfileID(string profileID, Action<CBSGetInventoryResult> result);

        /// <summary>
        /// Get inventory items list by specific category by profile id.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="category"></param>
        /// <param name="result"></param>
        void GetInventoryByCategoryByProfileID(string profileID, string category, Action<CBSGetInventoryResult> result);

        /// <summary>
        /// Modify uses count of item. For stackable item - modify stack count, for consumable - consume count. Pass a negative "count" value to decrease uses count.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="modifyCount"></param>
        /// <param name="result"></param>
        void ModifyUsesCount(string instanceId, int modifyCount, Action<CBSModifyItemUsesCountResult> result);

        /// <summary>
        /// Consume inventory item by item instance id.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="result"></param>
        void ConsumeItem(string instanceId, Action<CBSConsumeInventoryItemResult> result);

        /// <summary>
        /// Consume inventory item by item instance id.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="count"></param>
        /// <param name="result"></param>
        void ConsumeItem(string instanceId, int count, Action<CBSConsumeInventoryItemResult> result);

        /// <summary>
        /// Get full information of inventory item by instance id.
        /// </summary>
        /// <param name="instanceID"></param>
        /// <param name="result"></param>
        void GetItemByInstanceID(string instanceID, Action<CBSGetInventoryItemResult> result);

        /// <summary>
        /// Set unique data for item. For example, ID cells in the inventory. Not to be confused with Item Custom Data.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="dataKey"></param>
        /// <param name="dataValue"></param>
        /// <param name="result"></param>
        void SetItemDataByKey(string instanceId, string dataKey, string dataValue, Action<CBSSetInventoryDataResult> result);

        /// <summary>
        /// Equip item from inventory.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="result"></param>
        void EquipItem(string instanceId, Action<CBSChangeEquipStateResult> result);

        /// <summary>
        /// Unequip item from inventory
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="result"></param>
        void UnEquipItem(string instanceId, Action<CBSChangeEquipStateResult> result);

        /// <summary>
        /// Get loot bob list from inventory.
        /// </summary>
        /// <param name="result"></param>
        void GetLootboxes(Action<CBSGetLootboxesResult> result);

        /// <summary>
        /// Get lootboxes badge. Get count of not opened lootboxes.
        /// </summary>
        /// <param name="result"></param>
        void GetLootboxesBadge(Action<CBSBadgeResult> result);

        /// <summary>
        /// Get lootboxes badge from cache. Get count of not opened lootboxes.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        CBSBadgeResult GetLootboxesBadgeFromCache(Action<CBSBadgeResult> result = null);

        /// <summary>
        /// Get loot bob list from inventory by category.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="result"></param>
        void GetLootboxesByCategory(string category, Action<CBSGetLootboxesResult> result);

        /// <summary>
        /// Open loot box and get reward.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="result"></param>
        void OpenLootbox(string itemID, string instanceId, Action<CBSOpenLootboxResult> result);

        /// <summary>
        /// Unlock lootbox timer
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="result"></param>
        void UnlockLootboxTimer(string instanceId, Action<CBSUnlockLootboxTimerResult> result);

        /// <summary>
        /// Get last cached inventory.
        /// </summary>
        /// <returns></returns>
        CBSGetInventoryResult GetInventoryFromCache(Action<CBSGetInventoryResult> result = null);

        /// <summary>
        /// Get last cached loot box list
        /// </summary>
        /// <returns></returns>
        CBSGetLootboxesResult GetLootboxesFromCache(Action<CBSGetLootboxesResult> result = null);

        /// <summary>
        /// Get specific item from cache inventory.
        /// </summary>
        /// <param name="inventoryItemId"></param>
        /// <returns></returns>
        CBSInventoryItem GetInventoryItemFromCache(string inventoryItemId);

        /// <summary>
        /// Remove item from inventory of current auth profile
        /// </summary>
        /// <param name="instanceID"></param>
        /// <param name="result"></param>
        void RemoveInventoryItem(string instanceID, Action<CBSRemoveItemsResult> result);

        /// <summary>
        /// Remove items from inventory of current auth profile
        /// </summary>
        /// <param name="inventoryItemID"></param>
        /// <param name="result"></param>
        void RemoveInventoryItems(string[] instanceIDs, Action<CBSRemoveItemsResult> result);

        /// <summary>
        /// Remove item from inventory by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="instanceID"></param>
        /// <param name="result"></param>
        void RemoveInventoryItemFromProfile(string profileID, string instanceID, Action<CBSRemoveItemsResult> result);

        /// <summary>
        /// Remove items from inventory by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="inventoryItemID"></param>
        /// <param name="result"></param>
        void RemoveInventoryItemsFromProfile(string profileID, string[] instanceIDs, Action<CBSRemoveItemsResult> result);
    }
}
