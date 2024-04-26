using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabInventory : FabExecuter, IFabInventory
    {
        public void GetInventory(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileInventoryMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetItemByInventoryID(string profileID, string inventoryItemID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetItemByInventoryIDMethod,
                FunctionParameter = new FunctionKeyRequest
                {
                    ProfileID = profileID,
                    Key = inventoryItemID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetLootBoxes(string profileID, string lootboxesIDsRaw, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileLootboxesMethod,
                FunctionParameter = new FunctionGetLootboxesRequest
                {
                    ProfileID = profileID,
                    LootBoxesIDsRaw = lootboxesIDsRaw
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetLootBoxesBadge(string profileID, string lootboxesIDsRaw, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetLootboxesBadgeMethod,
                FunctionParameter = new FunctionGetLootboxesRequest
                {
                    ProfileID = profileID,
                    LootBoxesIDsRaw = lootboxesIDsRaw
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void SetItemEquipState(string profileID, string inventoryItemID, bool state, Action<ExecuteFunctionResult> onRemove, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.SetItemEquipStateMethod,
                FunctionParameter = new FunctionEquipStateRequest
                {
                    ProfileID = profileID,
                    State = state,
                    InventoryItemID = inventoryItemID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onRemove, onFailed);
        }

        public void RemokeInventoryItemsFromProfile(string profileID, string[] inventoryIDs, Action<ExecuteFunctionResult> onRemove, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.RemoveInventoryItemsFromProfileMethod,
                FunctionParameter = new FunctionRevokeItemsRequest
                {
                    ProfileID = profileID,
                    InstanceIDs = inventoryIDs
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onRemove, onFailed);
        }

        public void ConsumeItem(string profileID, string instanceID, int count, Action<ExecuteFunctionResult> onConsume, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ConsumeItemMethod,
                FunctionParameter = new FunctionModifyUsesRequest
                {
                    ProfileID = profileID,
                    ItemInstanceID = instanceID,
                    ModifyCount = count
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onConsume, onFailed);
        }

        public void ModifyItemUsesCount(string profileID, string instanceID, int count, Action<ExecuteFunctionResult> onModify, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ModifyItemUsesMethod,
                FunctionParameter = new FunctionModifyUsesRequest
                {
                    ProfileID = profileID,
                    ItemInstanceID = instanceID,
                    ModifyCount = count
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onModify, onFailed);
        }

        public void UpdateItemDataByKey(string profileID, string inventoryItemID, string dataKey, string dataValue, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateInventoryItemDataMethod,
                FunctionParameter = new FunctionUpdateItemDataRequest
                {
                    ProfileID = profileID,
                    DataKey = dataKey,
                    DataValue = dataValue,
                    InventoryItemID = inventoryItemID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void UnlockContainer(string profileID, string itemID, string inventoryItemID, Action<ExecuteFunctionResult> onUnlock, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UnlockContainerMethod,
                FunctionParameter = new OpenLootBoxRequest
                {
                    ProfileID = profileID,
                    ItemInstanceID = inventoryItemID,
                    ItemID = itemID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUnlock, onFailed);
        }
        
        public void UnlockLootboxTimer(string profileID, string inventoryItemID, Action<ExecuteFunctionResult> onUnlock, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UnlockLootboxTimerMethod,
                FunctionParameter = new FunctionKeyRequest()
                {
                    ProfileID = profileID,
                    Key = inventoryItemID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUnlock, onFailed);
        }
    }
}
