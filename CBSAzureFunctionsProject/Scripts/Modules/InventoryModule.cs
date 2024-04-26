using PlayFab.ServerModels;
using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using System.Collections.Generic;
using System.Linq;
using CBS.SharedData.Lootbox;
using System;

namespace CBS
{
    public class InventoryModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.GetProfileInventoryMethod)]
        public static async Task<dynamic> GetProfileInventoryTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();
            var profileID = request.ProfileID;

            var getResult = await GetProfileInventoryAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }
            var inventory = getResult.Result;
            return new FunctionGetInventoryResult
            {
                Instances = inventory.ToClientInstances()
            }.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileLootboxesMethod)]
        public static async Task<dynamic> GetProfileLootboxesTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetLootboxesRequest>();
            var profileID = request.ProfileID;
            var rawIDs = request.LootBoxesIDsRaw;

            var getResult = await GetProfileLootboxesAsync(profileID, rawIDs);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }
            var inventory = getResult.Result;
            return new FunctionGetInventoryResult
            {
                Instances = inventory.ToClientInstances()
            }.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetLootboxesBadgeMethod)]
        public static async Task<dynamic> GetLootboxesBadgeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetLootboxesRequest>();
            var profileID = request.ProfileID;
            var rawIDs = request.LootBoxesIDsRaw;

            var getResult = await GetLootboxesBadgeAsync(profileID, rawIDs);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }
            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SetItemEquipStateMethod)]
        public static async Task<dynamic> SetItemEquipStateTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionEquipStateRequest>();
            var profileID = request.ProfileID;
            var instanceID = request.InventoryItemID;
            var state = request.State;

            var changeResult = await ChangeItemEquipStateAsync(profileID, instanceID, state);
            if (changeResult.Error != null)
            {
                return ErrorHandler.ThrowError(changeResult.Error).AsFunctionResult();
            }
            return changeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateInventoryItemDataMethod)]
        public static async Task<dynamic> UpdateInventoryItemDataTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateItemDataRequest>();
            var profileID = request.ProfileID;
            var instanceID = request.InventoryItemID;
            var key = request.DataKey;
            var value = request.DataValue;

            var updateResult = await UpdateItemDataAndGetInstanceAsync(profileID, instanceID, key, value);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }
            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetItemByInventoryIDMethod)]
        public static async Task<dynamic> GetItemByInventoryIDTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionKeyRequest>();
            var profileID = request.ProfileID;
            var instanceID = request.Key;

            var getResult = await GetItemInstanceByInventoryIDAsync(profileID, instanceID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }
            var instance = getResult.Result;
            return new FunctionGetInventoryItemResult
            {
                Instance = instance.ToClientInstance()
            }.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ConsumeItemMethod)]
        public static async Task<dynamic> ConsumeItemTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionModifyUsesRequest>();
            var profileID = request.ProfileID;
            var inventoryItemID = request.ItemInstanceID;
            var consumeItem = request.ModifyCount;

            var consumeResult = await ConsumeItemAsync(profileID, inventoryItemID, consumeItem, true);
            if (consumeResult.Error != null)
            {
                return ErrorHandler.ThrowError(consumeResult.Error).AsFunctionResult();
            }

            return consumeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ModifyItemUsesMethod)]
        public static async Task<dynamic> ModifyItemUsesTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionModifyUsesRequest>();
            var profileID = request.ProfileID;
            var inventoryItemID = request.ItemInstanceID;
            var modifyCount = request.ModifyCount;

            var consumeResult = await ModifyUsesCountAsync(profileID, inventoryItemID, modifyCount, true);
            if (consumeResult.Error != null)
            {
                return ErrorHandler.ThrowError(consumeResult.Error).AsFunctionResult();
            }

            return consumeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.RemoveInventoryItemsFromProfileMethod)]
        public static async Task<dynamic> RemoveInventoryItemsFromProfileTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionRevokeItemsRequest>();
            var profileID = request.ProfileID;
            var instanceIDs = request.InstanceIDs;

            var revokeResult = await RevokeInventoryItemsFromProfileAsync(profileID, instanceIDs);
            if (revokeResult.Error != null)
            {
                return ErrorHandler.ThrowError(revokeResult.Error).AsFunctionResult();
            }
            return revokeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UnlockContainerMethod)]
        public static async Task<dynamic> UnlockContainerTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<OpenLootBoxRequest>();
            var profileID = request.ProfileID;
            var instanceID = request.ItemInstanceID;
            var itemID = request.ItemID;

            var revokeResult = await UnlockItemContainerAsync(profileID, itemID, instanceID);
            if (revokeResult.Error != null)
            {
                return ErrorHandler.ThrowError(revokeResult.Error).AsFunctionResult();
            }
            return revokeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UnlockLootboxTimerMethod)]
        public static async Task<dynamic> UnlockLootboxTimerTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionKeyRequest>();
            var profileID = request.ProfileID;
            var instanceID = request.Key;

            var revokeResult = await UnlockLootboxTimerAsync(profileID, instanceID);
            if (revokeResult.Error != null)
            {
                return ErrorHandler.ThrowError(revokeResult.Error).AsFunctionResult();
            }
            return revokeResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<List<ItemInstance>>> GetProfileInventoryAsync(string profileID)
        {
            var request = new GetUserInventoryRequest
            {
                PlayFabId = profileID
            };
            var result = await FabServerAPI.GetUserInventoryAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<List<ItemInstance>>(result.Error);
            }
            var inventory = result.Result.Inventory ?? new List<ItemInstance>();
            var catalogInventory = inventory.Where(x=>x.CatalogVersion == CatalogKeys.ItemsCatalogID).ToList();
            return new ExecuteResult<List<ItemInstance>>
            {
                Result = catalogInventory
            };
        }

        public static async Task<ExecuteResult<List<ItemInstance>>> GetProfileInventoryFromAllCatalogsAsync(string profileID)
        {
            var request = new GetUserInventoryRequest
            {
                PlayFabId = profileID
            };
            var result = await FabServerAPI.GetUserInventoryAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<List<ItemInstance>>(result.Error);
            }
            var inventory = result.Result.Inventory ?? new List<ItemInstance>();
            return new ExecuteResult<List<ItemInstance>>
            {
                Result = inventory
            };
        }

        public static async Task<ExecuteResult<List<ItemInstance>>> GetProfileLootboxesAsync(string profileID, string lootBoxesIDsRaw)
        {
            var lootBoxesIDs = new List<string>();
            try
            {
                lootBoxesIDs = JsonPlugin.FromJsonDecompress<List<string>>(lootBoxesIDsRaw);
            }
            catch {}
            var getInventoryResult = await GetProfileInventoryAsync(profileID);
            if (getInventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<ItemInstance>>(getInventoryResult.Error);
            }
            var inventory = getInventoryResult.Result;
            var lootboxes = inventory.Where(x=>lootBoxesIDs.Contains(x.ItemId)).ToList();
            return new ExecuteResult<List<ItemInstance>>
            {
                Result = lootboxes
            };
        }

        public static async Task<ExecuteResult<FunctionBadgeResult>> GetLootboxesBadgeAsync(string profileID, string lootBoxesIDsRaw)
        {
            var lootBoxesIDs = new List<string>();
            try
            {
                lootBoxesIDs = JsonPlugin.FromJsonDecompress<List<string>>(lootBoxesIDsRaw);
            }
            catch {}
            var getInventoryResult = await GetProfileInventoryAsync(profileID);
            if (getInventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBadgeResult>(getInventoryResult.Error);
            }
            var inventory = getInventoryResult.Result;
            var lootboxesCount = inventory.Where(x=>lootBoxesIDs.Contains(x.ItemId)).Count();
            return new ExecuteResult<FunctionBadgeResult>
            {
                Result = new FunctionBadgeResult
                {
                    Count = lootboxesCount
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEquipResult>> ChangeItemEquipStateAsync(string profileID, string inventoryItemID, bool state)
        {
            var updateDataResult = await UpdateInventoryItemCustomDataByKeyAsync(profileID, inventoryItemID, ItemDataKeys.InventoryEquippedKey, state.ToString());
            if (updateDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEquipResult>(updateDataResult.Error);
            }
            var instanceID = updateDataResult.Result;
            var getInstanceResult = await GetItemInstanceByInventoryIDAsync(profileID, instanceID);
            if (getInstanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEquipResult>(getInstanceResult.Error);
            }
            var fabInstance = getInstanceResult.Result;

            return new ExecuteResult<FunctionEquipResult>
            {
                Result = new FunctionEquipResult
                {
                    ProfileID = profileID,
                    ItemInstance = fabInstance.ToClientInstance(),
                    IsEquip = state
                }
            };
        }

        public static async Task<ExecuteResult<FunctionRevokeInventoryItemsResult>> RevokeInventoryItemsFromProfileAsync(string profileID, string[] instanceIds)
        {
            var revokeList = instanceIds.Select(x=> new RevokeInventoryItem
            {
                PlayFabId = profileID,
                ItemInstanceId = x
            }).ToList();
            var revokeRequest = new RevokeInventoryItemsRequest
            {
                Items = revokeList
            };
            var revokeResult = await FabServerAPI.RevokeInventoryItemsAsync(revokeRequest);
            if (revokeResult.Error != null)
            {
                ErrorHandler.ThrowError<FunctionRevokeInventoryItemsResult>(revokeResult.Error);
            }
            if (revokeResult.Result.Errors != null && revokeResult.Result.Errors.Any())
            {
                ErrorHandler.ItemInstanceNotFound<FunctionRevokeInventoryItemsResult>();
            }
            var errorList = revokeResult.Result.Errors;
            var errorIDs = errorList.Select(x=>x.Item.ItemInstanceId);
            var successList = instanceIds.Where(x=>!errorIDs.Contains(x)).ToArray();

            return new ExecuteResult<FunctionRevokeInventoryItemsResult>
            {
                Result = new FunctionRevokeInventoryItemsResult
                {
                    TargetID = profileID,
                    RevomedInstanceIDs = successList
                }
            };
        }

        public static async Task<ExecuteResult<FunctionModifyUsesResult>> ModifyUsesCountAsync(string profileID, string itemInstanceId, int countToAdd, bool fetchInstance = false)
        {
            var request = new ModifyItemUsesRequest
            {
                PlayFabId = profileID,
                ItemInstanceId = itemInstanceId,
                UsesToAdd = countToAdd
            };
            var modifyResult = await FabServerAPI.ModifyItemUsesAsync(request);
            if (modifyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyUsesResult>(modifyResult.Error);
            }
            var instanceID = modifyResult.Result.ItemInstanceId;
            var usesLeft = modifyResult.Result.RemainingUses;
            ItemInstance fabInstance = null;
            if (fetchInstance)
            {
                var getItemResult = await GetItemInstanceByInventoryIDAsync(profileID, instanceID);
                fabInstance = getItemResult.Result;
            }
            return new ExecuteResult<FunctionModifyUsesResult>
            {
                Result = new FunctionModifyUsesResult
                {
                    ProfileID = profileID,
                    UpdatedInstance = fabInstance.ToClientInstance(),
                    ItemInstanceID = instanceID,
                    UpdatedUsesCount = usesLeft
                }
            };
        }

        public static async Task<ExecuteResult<FunctionModifyUsesResult>> ConsumeItemAsync(string profileID, string itemInstanceId, int consumeCount, bool fetchInstance = false)
        {
            var request = new ConsumeItemRequest
            {
                PlayFabId = profileID,
                ItemInstanceId = itemInstanceId,
                ConsumeCount = consumeCount
            };
            var modifyResult = await FabServerAPI.ConsumeItemAsync(request);
            if (modifyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyUsesResult>(modifyResult.Error);
            }
            var instanceID = modifyResult.Result.ItemInstanceId;
            var usesLeft = modifyResult.Result.RemainingUses;
            ItemInstance fabInstance = null;
            if (fetchInstance)
            {
                var getItemResult = await GetItemInstanceByInventoryIDAsync(profileID, instanceID);
                fabInstance = getItemResult.Result;
            }
            return new ExecuteResult<FunctionModifyUsesResult>
            {
                Result = new FunctionModifyUsesResult
                {
                    ProfileID = profileID,
                    UpdatedInstance = fabInstance.ToClientInstance(),
                    ItemInstanceID = instanceID,
                    UpdatedUsesCount = usesLeft
                }
            };
        }

        public static async Task<ExecuteResult<string>> UpdateInventoryItemCustomDataByKeyAsync(string profileID, string inventoryItemID, string dataKey, string dataValue)
        {
            var updateResult = await UpdateItemInstanceDataAsync(profileID, inventoryItemID, dataKey, dataValue.ToString());
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<string>(updateResult.Error);
            }
            return new ExecuteResult<string>
            {
                Result = inventoryItemID
            };
        }

        public static async Task<ExecuteResult<FunctionUpdateItemDataResult>> UpdateItemDataAndGetInstanceAsync(string profileID, string inventoryItemID, string dataKey, string dataValue)
        {
            var updateResult = await UpdateInventoryItemCustomDataByKeyAsync(profileID, inventoryItemID, dataKey, dataValue);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateItemDataResult>(updateResult.Error);
            }
            var getInstanceResult = await GetItemInstanceByInventoryIDAsync(profileID, inventoryItemID);
            if (getInstanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateItemDataResult>(getInstanceResult.Error);
            }
            var fabInstance = getInstanceResult.Result;
            return new ExecuteResult<FunctionUpdateItemDataResult>
            {
                Result = new FunctionUpdateItemDataResult
                {
                    ProfileID = profileID,
                    Instance = fabInstance.ToClientInstance()
                }
            };
        }

        public static async Task<ExecuteResult<ItemInstance>> GetItemInstanceByInventoryIDAsync(string profileID, string inventoryItemID)
        {
            var getInventoryResult = await GetProfileInventoryAsync(profileID);
            if (getInventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<ItemInstance>(getInventoryResult.Error);
            }
            var inventory = getInventoryResult.Result;
            var fabInstance = inventory.FirstOrDefault(x=>x.ItemInstanceId == inventoryItemID);
            if (fabInstance == null)
            {
                return ErrorHandler.ItemInstanceNotFound<ItemInstance>();
            }
            return new ExecuteResult<ItemInstance>
            {
                Result = fabInstance
            };
        } 

        public static async Task<ExecuteResult<TransferItemResult>> TransferItemFromProfileToProfileAsync(string inventoryItemID, string ownerProfileID, string receiverProfileID)
        {
            var getInventoryItemResult = await GetItemInstanceByInventoryIDAsync(ownerProfileID, inventoryItemID);
            if (getInventoryItemResult.Error != null)
            {
                return ErrorHandler.ThrowError<TransferItemResult>(getInventoryItemResult.Error);
            }
            var itemInstance = getInventoryItemResult.Result;
            var itemID = itemInstance.ItemId;

            var removeResult = await RemoveProfileInventoryItem(ownerProfileID, inventoryItemID);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<TransferItemResult>(removeResult.Error);
            }

            var grantResult = new GrantItemsToProfileRequest
            {
                ProfileID = receiverProfileID,
                ItemsIDs = new string[] { itemID }
            };
            var grantItemResult = await ItemsModule.GrantItemsToProfileAsync(grantResult);
            if (grantItemResult.Error != null)
            {
                ErrorHandler.ThrowError<TransferItemResult>(grantItemResult.Error);
            }
            var grantedItemsResult = grantItemResult.Result;
            var items = grantedItemsResult.GrantedInstances;
            var transferedInstance = items.FirstOrDefault();
            if (transferedInstance == null)
            {
                return ErrorHandler.ItemInstanceNotFound<TransferItemResult>();
            }

            var copyResult = await CopyItemInstancePropertyAsync(itemInstance, transferedInstance.ToServerInstance(), receiverProfileID);
            if (copyResult.Error != null)
            {
                return ErrorHandler.ThrowError<TransferItemResult>(copyResult.Error);
            }
            var finalInstance = copyResult.Result;

            return new ExecuteResult<TransferItemResult>
            {
                Result = new TransferItemResult
                {
                    OwnerID = ownerProfileID,
                    ReceiverID = receiverProfileID,
                    Item = finalInstance
                }
            };
        }

        public static async Task<ExecuteResult<PlayFab.ClientModels.ItemInstance>> CopyItemInstancePropertyAsync(ItemInstance copyFrom, ItemInstance copyTo, string ownerProfileID)
        {
            var propertyArray = copyFrom.CustomData ?? new Dictionary<string, string>();
            var propertyToCopy = propertyArray.Where(x=>!ItemDataKeys.DontCopyProperties.Contains(x.Key)).ToDictionary(x=>x.Key, x=>x.Value);

            if (propertyToCopy.Count > 0)
            {
                var updateRequest = new UpdateUserInventoryItemDataRequest {
                    ItemInstanceId = copyTo.ItemInstanceId,
                    PlayFabId = ownerProfileID,
                    Data = propertyToCopy
                };

                var result = await FabServerAPI.UpdateUserInventoryItemCustomDataAsync(updateRequest);
                if (result.Error != null)
                {
                    return ErrorHandler.ThrowError<PlayFab.ClientModels.ItemInstance>(result.Error);
                }
            }

            var getInventoryItemResult = await GetItemInstanceByInventoryIDAsync(ownerProfileID, copyTo.ItemInstanceId);
            if (getInventoryItemResult.Error != null)
            {
                return ErrorHandler.ThrowError<PlayFab.ClientModels.ItemInstance>(getInventoryItemResult.Error);
            }
            var itemInstance = getInventoryItemResult.Result;

            return new ExecuteResult<PlayFab.ClientModels.ItemInstance>
            {
                Result = itemInstance.ToClientInstance()
            };
        }

        public static async Task<ExecuteResult<FunctionUnlockLootboxTimerResult>> UnlockLootboxTimerAsync(string profileID, string inventoryItemID)
        {
            var itemInstanceResult = await GetItemInstanceByInventoryIDAsync(profileID, inventoryItemID);
            if (itemInstanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUnlockLootboxTimerResult>(itemInstanceResult.Error);
            }
            var itemInstance = itemInstanceResult.Result;
            var itemID = itemInstance.ItemId;

            var getTableResult = await GetInternalTitleDataAsObjectAsync<CBSLootboxTable>(TitleKeys.LootboxTableKey);
            if (getTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUnlockLootboxTimerResult>(getTableResult.Error);
            }
            var lootBoxTable = getTableResult.Result;

            var containItem = lootBoxTable.ContainItem(itemID);
            if (!containItem)
            {
                return ErrorHandler.InvalidInput<FunctionUnlockLootboxTimerResult>();
            }

            var updateDataResult = await UpdateInventoryItemCustomDataByKeyAsync(profileID, inventoryItemID, ItemDataKeys.TimerUnlockKey, true.ToString());
            if (updateDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEquipResult>(updateDataResult.Error);
            }

            itemInstanceResult = await GetItemInstanceByInventoryIDAsync(profileID, inventoryItemID);
            if (itemInstanceResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUnlockLootboxTimerResult>(itemInstanceResult.Error);
            }
            itemInstance = itemInstanceResult.Result;

            return new ExecuteResult<FunctionUnlockLootboxTimerResult>
            {
                Result = new FunctionUnlockLootboxTimerResult
                {
                    UpdatedInstance = itemInstance.ToClientInstance()
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGrantItemsResult>> UnlockItemContainerAsync(string profileID, string itemID, string inventoryItemID)
        {
            var getGeneralDataResult = await GetInternalTitleDataAsObjectAsync<CBSLootboxGeneralData>(TitleKeys.LootboxGeneralDataKey);
            if (getGeneralDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantItemsResult>(getGeneralDataResult.Error);
            }
            var generalData = getGeneralDataResult.Result;
            var lootboxSolution = generalData.Solution;
            if (lootboxSolution == LootboxSolution.PLAYFAB)
            {
                var openResult = await UnlockPlayFabContainerAsync(profileID, inventoryItemID);
                if (openResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGrantItemsResult>(openResult.Error);
                }
                return openResult;
            }
            else
            {
                var openResult = await UnlockCBSCustomContainerAsync(profileID, itemID, inventoryItemID);
                if (openResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGrantItemsResult>(openResult.Error);
                }
                return openResult;
            }
        }

        public static async Task<ExecuteResult<FunctionGrantItemsResult>> UnlockPlayFabContainerAsync(string profileID, string inventoryItemID)
        {
            var request = new UnlockContainerInstanceRequest
            {
                PlayFabId = profileID,
                ContainerItemInstanceId = inventoryItemID
            };
            var unlockResult = await FabServerAPI.UnlockContainerInstanceAsync(request);
            if (unlockResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantItemsResult>(unlockResult.Error);
            }
            var unclockBundle = unlockResult.Result;
            var grantedItems = unclockBundle.GrantedItems;
            var grantedCurrencies = unclockBundle.VirtualCurrency;

            return new ExecuteResult<FunctionGrantItemsResult>
            {
                Result = new FunctionGrantItemsResult
                {
                    TargetID = profileID,
                    GrantedInstances = grantedItems.ToClientInstances(),
                    GrantedCurrencies = grantedCurrencies
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGrantItemsResult>> UnlockCBSCustomContainerAsync(string profileID, string itemID, string inventoryItemID)
        {
            var getTableResult = await GetInternalTitleDataAsObjectAsync<CBSLootboxTable>(TitleKeys.LootboxTableKey);
            if (getTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantItemsResult>(getTableResult.Error);
            }
            var lootBoxTable = getTableResult.Result;

            var containItem = lootBoxTable.ContainItem(itemID);
            if (!containItem)
            {
                return ErrorHandler.InvalidInput<FunctionGrantItemsResult>();
            }

            var lootEntity = lootBoxTable.GetDropEntityForItem(itemID);
            if (lootEntity == null)
            {
                return ErrorHandler.InvalidInput<FunctionGrantItemsResult>();
            }
            var timerLocked = lootEntity.TimerLocked;
            if (timerLocked)
            {
                var itemInstanceResult = await GetItemInstanceByInventoryIDAsync(profileID, inventoryItemID);
                if (itemInstanceResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGrantItemsResult>(itemInstanceResult.Error);
                }
                var itemInstance = itemInstanceResult.Result;
                var customData = itemInstance.CustomData ?? new Dictionary<string, string>();
                var forceUnlocked = customData.ContainsKey(ItemDataKeys.TimerUnlockKey);
                var purchaseDate = itemInstance.PurchaseDate;
                if (purchaseDate != null && !forceUnlocked)
                {
                    var timerSeconds = lootEntity.LockSeconds;
                    var unlockDate = purchaseDate.GetValueOrDefault().AddSeconds(timerSeconds);
                    var timerSpan = unlockDate - ServerTimeUTC;
                    var totalSecondsLeft = timerSpan.Seconds;
                    var locked = totalSecondsLeft > 0;
                    if (locked)
                    {
                        return ErrorHandler.LootboxNotAvalilable<FunctionGrantItemsResult>();
                    }
                }
            }

            var revokeItemResult = await RemoveProfileInventoryItem(profileID, inventoryItemID);
            if (revokeItemResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantItemsResult>(revokeItemResult.Error);
            }

            var grantResult = await GrantItemsFromLootEntityAsync(profileID, lootEntity);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantItemsResult>(grantResult.Error);
            }

            return grantResult;
        }

        private static async Task<ExecuteResult<FunctionGrantItemsResult>> GrantItemsFromLootEntityAsync(string playerID, LootBoxEntity lootEntity)
        {
            var slots = lootEntity.Slots ?? new List<DropSlot>();
            Random random = new Random();
            var itemsIdsToGrant = new List<string>();
            var currenciesToGrant = new Dictionary<string, int>();
            foreach(var slot in slots)
            {
                var dropBehavior = slot.DropBehavior;
                var rewardType = slot.SlotDropType;
                if (dropBehavior == DropBehavior.BY_CHANGE)
                {
                    var dropChance = slot.DropChance;
                    
                    float randomFloat = (float)(random.NextDouble() * 100f);
                    if (randomFloat > dropChance)
                        continue;
                }

                if (rewardType == SlotDropType.ITEM)
                {
                    var dropItems = slot.ItemsToDrop ?? new List<DropItem>();
                    if (!dropItems.Any())
                        continue;
                    var totalWeight = dropItems.Sum(item => item.Weight);
                    var randomNumber = random.Next(totalWeight);
                    DropItem droppedItem = null;
                    var cumulativeWeight = 0;
                    foreach (var item in dropItems)
                    {
                        cumulativeWeight += item.Weight;
                        if (randomNumber < cumulativeWeight)
                        {
                            droppedItem = item;
                            break;
                        }
                    }
                    if (droppedItem != null)
                    {
                        itemsIdsToGrant.Add(droppedItem.ItemID);
                    }
                }
                else
                {
                    var dropCurrencies = slot.CurrenciesToDrop ?? new List<DropCurrency>();
                    foreach (var dropCurrency in dropCurrencies)
                    {
                        var currencyDropBehavior = dropCurrency.DropType;
                        var currencyCode = dropCurrency.CurrencyCode;
                        if (currencyDropBehavior == CurrencyDropType.MIN_MAX_VALUE)
                        {
                            var minValue = dropCurrency.Min;
                            var maxValue = dropCurrency.Max;
                            if (minValue > maxValue)
                            {
                                maxValue = minValue;
                            }
                            var randomValue = random.Next(minValue, maxValue + 1);
                            currenciesToGrant[currencyCode] = randomValue;
                        }
                        else if (currencyDropBehavior == CurrencyDropType.VALUE_DROP_CHANCE)
                        {
                            var dropTable = dropCurrency.DropTable;
                            if (dropTable == null || !dropTable.Any())
                                continue;
                            var totalWeight = dropTable.Sum(item => item.Weight);
                            var randomNumber = random.Next(totalWeight);
                            var valueToDrop = 0;
                            var cumulativeWeight = 0;
                            foreach (var item in dropTable)
                            {
                                cumulativeWeight += item.Weight;
                                if (randomNumber < cumulativeWeight)
                                {
                                    valueToDrop = item.Amount;
                                    break;
                                }
                            }
                            if (valueToDrop != 0)
                            {
                                currenciesToGrant[currencyCode] = valueToDrop;
                            }
                        }
                    }
                }
            }

            List<PlayFab.ClientModels.ItemInstance> grantedInstances = new List<PlayFab.ClientModels.ItemInstance>();

            if (itemsIdsToGrant.Any())
            {
                var grantRequest = new GrantItemsToProfileRequest
                {
                    ProfileID = playerID,
                    ItemsIDs = itemsIdsToGrant.ToArray()
                };
                var grantResult = await ItemsModule.GrantItemsToProfileAsync(grantRequest);
                if (grantResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGrantItemsResult>(grantResult.Error);
                }
                grantedInstances = grantResult.Result.GrantedInstances;
            }

            Dictionary<string, uint> grantedCurrencies = new Dictionary<string, uint>();

            foreach (var currencyPair in currenciesToGrant)
            {
                var grantCurrencyResult = await CurrencyModule.AddVirtualCurrencyToProfileAsync(playerID, currencyPair.Key, currencyPair.Value);
                if (grantCurrencyResult.Error == null)
                {
                    grantedCurrencies[currencyPair.Key] = (uint)currencyPair.Value;
                }
            }

            return new ExecuteResult<FunctionGrantItemsResult>
            {
                Result = new FunctionGrantItemsResult
                {
                    GrantedCurrencies = grantedCurrencies,
                    GrantedInstances = grantedInstances
                }
            };         
        }
    }
}