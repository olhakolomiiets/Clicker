using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public interface IFabInventory
    {
        void GetInventory(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void GetItemByInventoryID(string profileID, string inventoryItemID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void GetLootBoxes(string profileID, string lootboxesIDsRaw, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void GetLootBoxesBadge(string profileID, string lootboxesIDsRaw, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
        void SetItemEquipState(string profileID, string inventoryItemID, bool state, Action<ExecuteFunctionResult> onRemove, Action<PlayFabError> onFailed);
        void RemokeInventoryItemsFromProfile(string profileID, string[] inventoryIDs, Action<ExecuteFunctionResult> onRemove, Action<PlayFabError> onFailed);
        void ConsumeItem(string profileID, string instanceID, int count, Action<ExecuteFunctionResult> onConsume, Action<PlayFabError> onFailed);
        void ModifyItemUsesCount(string profileID, string instanceID, int count, Action<ExecuteFunctionResult> onModify, Action<PlayFabError> onFailed);
        void UpdateItemDataByKey(string profileID, string inventoryItemID, string dataKey, string dataValue, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);
        void UnlockContainer(string profileID, string itemID, string inventoryItemID, Action<ExecuteFunctionResult> onUnlock, Action<PlayFabError> onFailed);
        void UnlockLootboxTimer(string profileID, string inventoryItemID, Action<ExecuteFunctionResult> onUnlock, Action<PlayFabError> onFailed);
    }
}
