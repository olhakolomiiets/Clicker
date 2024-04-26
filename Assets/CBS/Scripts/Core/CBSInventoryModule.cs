using CBS.Models;
using CBS.Playfab;
using CBS.Scriptable;
using CBS.Utils;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSInventoryModule : CBSModule, ICBSInventory
    {
        private readonly string ItemsCatalog = CatalogKeys.ItemsCatalogID;

        /// <summary>
        ///Notify when uses count changed for item.
        /// </summary>
        public event Action<ItemUsesCountChange> OnItemUsageCountChange;
        /// <summary>
        /// Notify when item was equipped.
        /// </summary>
        public event Action<CBSInventoryItem> OnItemEquiped;
        /// <summary>
        /// Notify when item was unequipped.
        /// </summary>
        public event Action<CBSInventoryItem> OnItemUnEquiped;
        /// <summary>
        /// Notify when item was added to inventory.
        /// </summary>
        public event Action<CBSInventoryItem> OnItemAdded;
        /// <summary>
        /// Notify when item was removed from inventory.
        /// </summary>
        public event Action<string> OnItemRemoved;
        /// <summary>
        /// Notify when loot box was added to inventory.
        /// </summary>
        public event Action<CBSInventoryItem> OnLootboxAdded;
        /// <summary>
        /// Notifies when a user has opened a lootbox
        /// </summary>
        public event Action<LootboxBundle> OnLootboxOpen;
        /// <summary>
        /// Notifies when a user has opened a lootbox
        /// </summary>
        public event Action<CBSLootboxInventoryItem> OnLootboxTimerUnlocked;

        private IAuth Auth { get; set; }
        private IFabInventory FabInventory { get; set; }
        private IProfile Profile { get; set; }
        private ICBSItems Items { get; set; }
        private AuthData AuthData { get; set; }

        public Dictionary<string, CBSInventoryItem> InventoryCache { get; private set; }
        public Dictionary<string, CBSLootboxInventoryItem> LootBoxCache { get; private set; }

        protected override void Init()
        {
            Auth = Get<CBSAuthModule>();
            FabInventory = FabExecuter.Get<FabInventory>();
            AuthData = CBSScriptable.Get<AuthData>();
            Profile = Get<CBSProfileModule>();
            Items = Get<CBSItemsModule>();

            InventoryCache = new Dictionary<string, CBSInventoryItem>();
            LootBoxCache = new Dictionary<string, CBSLootboxInventoryItem>();
            Auth.OnLoginEvent += OnLoginSuccess;
        }

        // public API

        /// <summary>
        /// Get inventory items list of current profile.
        /// </summary>
        /// <param name="OnGetResult"></param>
        public void GetInventory(Action<CBSGetInventoryResult> result)
        {
            var profileID = Profile.ProfileID;
            InternalGetProfileInventory(profileID, null, result);
        }

        /// <summary>
        /// Get inventory items list of current profile by specific category.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="OnGetResult"></param>
        public void GetInventoryByCategory(string category, Action<CBSGetInventoryResult> result)
        {
            var profileID = Profile.ProfileID;
            InternalGetProfileInventory(profileID, category, result);
        }

        /// <summary>
        /// Get inventory items list by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        public void GetProfileInventoryByProfileID(string profileID, Action<CBSGetInventoryResult> result)
        {
            InternalGetProfileInventory(profileID, null, result);
        }

        /// <summary>
        /// Get inventory items list by specific category by profile id.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="category"></param>
        /// <param name="result"></param>
        public void GetInventoryByCategoryByProfileID(string profileID, string category, Action<CBSGetInventoryResult> result)
        {
            InternalGetProfileInventory(profileID, category, result);
        }

        /// <summary>
        /// Get last cached inventory.
        /// </summary>
        /// <returns></returns>
        public CBSGetInventoryResult GetInventoryFromCache(Action<CBSGetInventoryResult> result = null)
        {
            var profileID = Profile.ProfileID;
            var instances = InventoryCache.Select(x => x.Value).ToList();
            var resultObject = new CBSGetInventoryResult(instances, profileID);
            resultObject.IsSuccess = true;
            result?.Invoke(resultObject);
            return resultObject;
        }

        /// <summary>
        /// Get full information of inventory item by instance id.
        /// </summary>
        /// <param name="inventoryItemID"></param>
        /// <param name="result"></param>
        public void GetItemByInstanceID(string inventoryItemID, Action<CBSGetInventoryItemResult> result)
        {
            string profileID = Profile.ProfileID;
            FabInventory.GetItemByInventoryID(profileID, inventoryItemID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetInventoryItemResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetInventoryItemResult>();
                    var fabInstance = functionResult.Instance;
                    var cbsInstance = fabInstance.ToCBSInventoryItem();
                    result?.Invoke(new CBSGetInventoryItemResult
                    {
                        IsSuccess = true,
                        InventoryItem = cbsInstance
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetInventoryItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Remove item from inventory of current auth profile
        /// </summary>
        /// <param name="instanceID"></param>
        /// <param name="result"></param>
        public void RemoveInventoryItem(string instanceID, Action<CBSRemoveItemsResult> result)
        {
            string profileID = Profile.ProfileID;
            InternalRevokeInventoryItems(profileID, new string[] { instanceID }, result);
        }

        /// <summary>
        /// Remove items from inventory of current auth profile
        /// </summary>
        /// <param name="inventoryItemID"></param>
        /// <param name="result"></param>
        public void RemoveInventoryItems(string[] instanceIDs, Action<CBSRemoveItemsResult> result)
        {
            string profileID = Profile.ProfileID;
            InternalRevokeInventoryItems(profileID, instanceIDs, result);
        }

        /// <summary>
        /// Remove item from inventory by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="instanceItemID"></param>
        /// <param name="result"></param>
        public void RemoveInventoryItemFromProfile(string profileID, string instanceItemID, Action<CBSRemoveItemsResult> result)
        {
            InternalRevokeInventoryItems(profileID, new string[] { instanceItemID }, result);
        }

        /// <summary>
        /// Remove items from inventory by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="inventoryItemID"></param>
        /// <param name="result"></param>
        public void RemoveInventoryItemsFromProfile(string profileID, string[] instanceIDs, Action<CBSRemoveItemsResult> result)
        {
            InternalRevokeInventoryItems(profileID, instanceIDs, result);
        }

        /// <summary>
        /// Modify uses count of item. For stackable item - modify stack count, for consumable - consume count. Pass a negative "count" value to decrease uses count.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="modifyCount"></param>
        /// <param name="result"></param>
        public void ModifyUsesCount(string instanceId, int modifyCount, Action<CBSModifyItemUsesCountResult> result)
        {
            var profileID = Profile.ProfileID;
            InternalModifyItemUsesCount(profileID, instanceId, modifyCount, result);
        }

        /// <summary>
        /// Consume inventory item by item instance id.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="result"></param>
        public void ConsumeItem(string instanceId, Action<CBSConsumeInventoryItemResult> result)
        {
            var profileID = Profile.ProfileID;
            InternalConsumeItem(profileID, instanceId, 1, result);
        }

        /// <summary>
        /// Consume inventory item by item instance id.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="count"></param>
        /// <param name="result"></param>
        public void ConsumeItem(string instanceId, int count, Action<CBSConsumeInventoryItemResult> result)
        {
            var profileID = Profile.ProfileID;
            InternalConsumeItem(profileID, instanceId, count, result);
        }

        /// <summary>
        /// Get loot boxes list from inventory.
        /// </summary>
        /// <param name="result"></param>
        public void GetLootboxes(Action<CBSGetLootboxesResult> result)
        {
            var profileID = Profile.ProfileID;
            InternalGetProfileLootboxes(profileID, null, result);
        }

        /// <summary>
        /// Get lootboxes badge. Get count of not opened lootboxes.
        /// </summary>
        /// <param name="result"></param>
        public void GetLootboxesBadge(Action<CBSBadgeResult> result)
        {
            var profileID = Profile.ProfileID;
            var allLootboxes = Items.AllLootboxes ?? new List<CBSLootbox>();
            var allLootboxesIDs = allLootboxes.Select(x => x.ItemID);
            var rawIDs = JsonPlugin.ToJsonCompress(allLootboxesIDs);
            FabInventory.GetLootBoxesBadge(profileID, rawIDs, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionBadgeResult>();
                    var count = functionResult.Count;
                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = true,
                        Count = count
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBadgeResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get lootboxes badge from cache. Get count of not opened lootboxes.
        /// </summary>
        /// <param name="result"></param>
        public CBSBadgeResult GetLootboxesBadgeFromCache(Action<CBSBadgeResult> result = null)
        {
            var inventoryCache = GetInventoryFromCache();
            var lootboxes = inventoryCache.Lootboxes ?? new List<CBSInventoryItem>();
            var count = lootboxes.Count;
            var resultObject = new CBSBadgeResult
            {
                IsSuccess = true,
                Count = count
            };
            result?.Invoke(resultObject);
            return resultObject;
        }

        /// <summary>
        /// Get loot boxes list from inventory by category.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="result"></param>
        public void GetLootboxesByCategory(string category, Action<CBSGetLootboxesResult> result)
        {
            var profileID = Profile.ProfileID;
            InternalGetProfileLootboxes(profileID, category, result);
        }

        /// <summary>
        /// Get last cached loot box list
        /// </summary>
        /// <returns></returns>
        public CBSGetLootboxesResult GetLootboxesFromCache(Action<CBSGetLootboxesResult> result = null)
        {
            var profileID = Profile.ProfileID;
            var instances = LootBoxCache.Select(x => x.Value).Where(x => x.Type == ItemType.LOOT_BOXES).ToList();
            var resultObject = new CBSGetLootboxesResult
            {
                IsSuccess = true,
                Lootboxes = instances
            };
            result?.Invoke(resultObject);
            return resultObject;
        }

        /// <summary>
        /// Equip item from inventory.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="result"></param>
        public void EquipItem(string instanceId, Action<CBSChangeEquipStateResult> result)
        {
            string profileID = Profile.ProfileID;
            InternalEquipUnequipItem(profileID, instanceId, true, result);
        }

        /// <summary>
        /// Unequip item from inventory
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="result"></param>
        public void UnEquipItem(string instanceId, Action<CBSChangeEquipStateResult> result)
        {
            string profileID = Profile.ProfileID;
            InternalEquipUnequipItem(profileID, instanceId, false, result);
        }

        /// <summary>
        /// Set unique data for item. For example, ID cells in the inventory. Not to be confused with Item Custom Data.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="dataKey"></param>
        /// <param name="dataValue"></param>
        /// <param name="result"></param>
        public void SetItemDataByKey(string instanceId, string dataKey, string dataValue, Action<CBSSetInventoryDataResult> result)
        {
            var profileID = Profile.ProfileID;
            FabInventory.UpdateItemDataByKey(profileID, instanceId, dataKey, dataValue,
                onUpdate =>
                {
                    var cbsError = onUpdate.GetCBSError();
                    if (cbsError != null)
                    {
                        var callback = new CBSSetInventoryDataResult
                        {
                            IsSuccess = false,
                            Error = cbsError
                        };
                        result?.Invoke(callback);
                    }
                    else
                    {
                        var functionResult = onUpdate.GetResult<FunctionUpdateItemDataResult>();
                        var fabInstance = functionResult.Instance;
                        var cbsInstance = functionResult.Instance.ToCBSInventoryItem();
                        UpdateRequest(cbsInstance);
                        var callback = new CBSSetInventoryDataResult
                        {
                            IsSuccess = true,
                            InventoryItem = cbsInstance
                        };
                        result?.Invoke(callback);
                    }
                }, onFailed =>
                {
                    var callback = new CBSSetInventoryDataResult
                    {
                        IsSuccess = false,
                        Error = CBSError.FromTemplate(onFailed)
                    };
                    result?.Invoke(callback);
                });
        }

        /// <summary>
        /// Open loot box and get reward.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="result"></param>
        public void OpenLootbox(string itemID, string instanceId, Action<CBSOpenLootboxResult> result)
        {
            var profileID = Profile.ProfileID;
            FabInventory.UnlockContainer(profileID, itemID, instanceId, onOpen =>
            {
                var cbsError = onOpen.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSOpenLootboxResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onOpen.GetResult<FunctionGrantItemsResult>();
                    var fabItems = functionResult.GrantedInstances;
                    var items = fabItems.Select(x => x.ToCBSInventoryItem()).ToList();
                    var currencies = functionResult.GrantedCurrencies;
                    OnLootboxOpen?.Invoke(new LootboxBundle
                    {
                        GrantedItems = items,
                        Currencies = currencies
                    });
                    // inventory request change
                    foreach (var t in items)
                        AddRequest(t);
                    // currency request change
                    Get<CBSCurrencyModule>().ChangeRequest(currencies.Select(x => x.Key).ToArray());
                    result?.Invoke(new CBSOpenLootboxResult
                    {
                        IsSuccess = true,
                        GrantedItems = items,
                        Currencies = currencies
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSOpenLootboxResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }
        
        /// <summary>
        /// Unlock lootbox timer
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="result"></param>
        public void UnlockLootboxTimer(string instanceId, Action<CBSUnlockLootboxTimerResult> result)
        {
            var profileID = Profile.ProfileID;
            FabInventory.UnlockLootboxTimer(profileID, instanceId, onOpen =>
            {
                var cbsError = onOpen.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSUnlockLootboxTimerResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onOpen.GetResult<FunctionUnlockLootboxTimerResult>();
                    var updatedInstance = functionResult.UpdatedInstance;
                    var cbsInstance = updatedInstance.ToCBSInventoryItem();
                    var cbsLootbox = cbsInstance.ToCBSLootboxInventoryItem();
                    OnLootboxTimerUnlocked?.Invoke(cbsLootbox);
                    // inventory request change
                    UpdateRequest(cbsInstance);
                    result?.Invoke(new CBSUnlockLootboxTimerResult
                    {
                        IsSuccess = true,
                        UpdatedInstance = cbsLootbox
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSUnlockLootboxTimerResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get specific item from cache inventory.
        /// </summary>
        /// <param name="inventoryItemId"></param>
        /// <returns></returns>
        public CBSInventoryItem GetInventoryItemFromCache(string inventoryItemId)
        {
            try
            {
                return InventoryCache[inventoryItemId];
            }
            catch
            {
                return null;
            }
        }

        // internal
        private void InternalGetProfileInventory(string profileID, string specificCategory, Action<CBSGetInventoryResult> result)
        {
            var authProfileID = Profile.ProfileID;

            FabInventory.GetInventory(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetInventoryResult()
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetInventoryResult>();
                    var instances = functionResult.Instances;
                    if (authProfileID == profileID)
                    {
                        ParseInventory(instances);
                    }
                    var cbsInstances = instances.Select(x => x.ToCBSInventoryItem()).ToList();
                    if (!string.IsNullOrEmpty(specificCategory))
                    {
                        cbsInstances = cbsInstances.Where(x => x.Category == specificCategory).ToList();
                    }
                    var resultObject = new CBSGetInventoryResult(cbsInstances, profileID);
                    resultObject.IsSuccess = true;
                    result?.Invoke(resultObject);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetInventoryResult()
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        private void InternalGetProfileLootboxes(string profileID, string specificCategory, Action<CBSGetLootboxesResult> result)
        {
            var authProfileID = Profile.ProfileID;
            var allLootboxes = Items.AllLootboxes ?? new List<CBSLootbox>();
            var allLootboxesIDs = allLootboxes.Select(x => x.ItemID);
            var rawIDs = JsonPlugin.ToJsonCompress(allLootboxesIDs);

            FabInventory.GetLootBoxes(profileID, rawIDs, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetLootboxesResult()
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetInventoryResult>();
                    var instances = functionResult.Instances;
                    var cbsInstances = instances.Select(x => x.ToCBSLootboxInventoryItem()).ToList();
                    if (!string.IsNullOrEmpty(specificCategory))
                    {
                        cbsInstances = cbsInstances.Where(x => x.Category == specificCategory).ToList();
                    }

                    result?.Invoke(new CBSGetLootboxesResult
                    {
                        IsSuccess = true,
                        Lootboxes = cbsInstances
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetLootboxesResult()
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        public void InternalConsumeItem(string profileID, string inventoryItemID, int consumeCount, Action<CBSConsumeInventoryItemResult> result)
        {
            var authProfileID = Profile.ProfileID;
            FabInventory.ConsumeItem(profileID, inventoryItemID, consumeCount, onConsume =>
            {
                var cbsError = onConsume.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSConsumeInventoryItemResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onConsume.GetResult<FunctionModifyUsesResult>();
                    var targetProfileID = functionResult.ProfileID;
                    var inventoryID = functionResult.ItemInstanceID;
                    var updatedCount = functionResult.UpdatedUsesCount;
                    var fabItem = functionResult.UpdatedInstance;
                    var cbsItem = fabItem?.ToCBSInventoryItem();
                    if (authProfileID == profileID)
                    {
                        if (cbsItem != null)
                        {
                            UpdateRequest(cbsItem);
                        }
                        else
                        {
                            if (updatedCount == 0)
                            {
                                RevokeRequest(inventoryID);
                            }
                            cbsItem = GetInventoryItemFromCache(inventoryItemID);
                            cbsItem?.SetUsageCount(updatedCount);
                        }
                        OnItemUsageCountChange?.Invoke(new ItemUsesCountChange
                        {
                            ItemInventoryID = inventoryID,
                            Removed = updatedCount == 0,
                            ChangedItem = cbsItem,
                            UsesLeft = updatedCount
                        });
                    }
                    var resultObject = new CBSConsumeInventoryItemResult
                    {
                        IsSuccess = true,
                        InstanceId = inventoryID,
                        ProfileID = targetProfileID,
                        ConsumedItem = cbsItem,
                        CountLeft = updatedCount
                    };
                    result?.Invoke(resultObject);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSConsumeInventoryItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        public void InternalModifyItemUsesCount(string profileID, string inventoryItemID, int countToModify, Action<CBSModifyItemUsesCountResult> result)
        {
            var authProfileID = Profile.ProfileID;
            FabInventory.ModifyItemUsesCount(profileID, inventoryItemID, countToModify, onConsume =>
            {
                var cbsError = onConsume.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyItemUsesCountResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onConsume.GetResult<FunctionModifyUsesResult>();
                    var targetProfileID = functionResult.ProfileID;
                    var inventoryID = functionResult.ItemInstanceID;
                    var updatedCount = functionResult.UpdatedUsesCount;
                    var fabItem = functionResult.UpdatedInstance;
                    var cbsItem = fabItem?.ToCBSInventoryItem();
                    if (authProfileID == profileID)
                    {
                        if (cbsItem != null)
                        {
                            UpdateRequest(cbsItem);
                        }
                        else
                        {
                            if (updatedCount == 0)
                            {
                                RevokeRequest(inventoryID);
                            }
                            cbsItem = GetInventoryItemFromCache(inventoryItemID);
                            cbsItem?.SetUsageCount(updatedCount);
                        }
                        OnItemUsageCountChange?.Invoke(new ItemUsesCountChange
                        {
                            ItemInventoryID = inventoryID,
                            Removed = updatedCount == 0,
                            ChangedItem = cbsItem,
                            UsesLeft = updatedCount
                        });
                    }
                    var resultObject = new CBSModifyItemUsesCountResult
                    {
                        IsSuccess = true,
                        InstanceId = inventoryID,
                        ProfileID = targetProfileID,
                        UpdatedCount = updatedCount,
                        UpdatedItem = cbsItem
                    };
                    result?.Invoke(resultObject);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyItemUsesCountResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        private void InternalEquipUnequipItem(string profileID, string inventoryItemId, bool equip, Action<CBSChangeEquipStateResult> result)
        {
            var authProfileID = Profile.ProfileID;

            FabInventory.SetItemEquipState(profileID, inventoryItemId, equip, onChange =>
            {
                var cbsError = onChange.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSChangeEquipStateResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onChange.GetResult<FunctionEquipResult>();
                    var fabInstance = functionResult.ItemInstance;
                    var cbsInstance = fabInstance.ToCBSInventoryItem();
                    var state = functionResult.IsEquip;
                    if (authProfileID == profileID)
                    {
                        if (equip)
                        {
                            OnItemEquiped?.Invoke(cbsInstance);
                        }
                        else
                        {
                            OnItemUnEquiped?.Invoke(cbsInstance);
                        }
                    }
                    result?.Invoke(new CBSChangeEquipStateResult
                    {
                        IsSuccess = true,
                        InventoryItem = cbsInstance,
                        IsEquip = state
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSChangeEquipStateResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        private void InternalRevokeInventoryItems(string profileID, string[] inventoryItemsIDs, Action<CBSRemoveItemsResult> result)
        {
            var authProfileID = Profile.ProfileID;
            FabInventory.RemokeInventoryItemsFromProfile(profileID, inventoryItemsIDs, onRemove =>
            {
                var cbsError = onRemove.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSRemoveItemsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onRemove.GetResult<FunctionRevokeInventoryItemsResult>();
                    var itemIDs = functionResult.RevomedInstanceIDs;
                    var targetProfileID = functionResult.TargetID;

                    if (authProfileID == targetProfileID)
                    {
                        RevokeRequest(itemIDs.ToList());
                    }

                    result?.Invoke(new CBSRemoveItemsResult
                    {
                        IsSuccess = true,
                        ProfileID = profileID,
                        RemovedInstanceItemsIDs = itemIDs
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSRemoveItemsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        internal List<CBSInventoryItem> ParseInventory(List<ItemInstance> items)
        {
            items = items.Where(x => x.CatalogVersion == ItemsCatalog).ToList();
            var inventoryItems = items.Select(x => x.ToCBSInventoryItem()).ToList();
            inventoryItems = inventoryItems.Where(x => x.IsInTrading == false).ToList();
            InventoryCache = inventoryItems.ToDictionary(x => x.InstanceID, x => x);
            LootBoxCache = items.Where(x => x.ToCBSInventoryItem().Type == ItemType.LOOT_BOXES)
                .Select(x => x.ToCBSLootboxInventoryItem()).ToDictionary(x=>x.InstanceID, x=>x);
            return inventoryItems;
        }

        internal void AddRequest(CBSInventoryItem item)
        {
            var inventoryId = item.InstanceID;
            InventoryCache[inventoryId] = item;

            if (item.Type == ItemType.ITEMS)
            {
                OnItemAdded?.Invoke(item);
            }
            else if (item.Type == ItemType.LOOT_BOXES)
            {
                LootBoxCache[inventoryId] = item.ToCBSLootboxInventoryItem();
                OnLootboxAdded?.Invoke(item);
            }
        }

        internal void AddRequest(List<CBSInventoryItem> items)
        {
            foreach (var item in items)
                AddRequest(item);
        }

        internal void UpdateRequest(List<CBSInventoryItem> items)
        {
            foreach (var item in items)
                UpdateRequest(item);
        }

        internal void UpdateRequest(CBSInventoryItem item)
        {
            var inventoryId = item.InstanceID;
            InventoryCache[inventoryId] = item;
            if (item.Type == ItemType.LOOT_BOXES)
            {
                LootBoxCache[inventoryId] = item.ToCBSLootboxInventoryItem();
            }
        }

        internal void RevokeRequest(string itemInventoryID)
        {
            var exist = InventoryCache.ContainsKey(itemInventoryID);
            if (exist)
            {
                InventoryCache.Remove(itemInventoryID);
            }
            var lootBoxExist = LootBoxCache.ContainsKey(itemInventoryID);
            if (lootBoxExist)
            {
                LootBoxCache.Remove(itemInventoryID);
            }
            OnItemRemoved?.Invoke(itemInventoryID);
        }

        internal void RevokeRequest(List<string> itemsIDs)
        {
            foreach (var item in itemsIDs)
                RevokeRequest(item);
        }

        internal void ConsumeSpendRequest(string inventoryID, uint spendCount)
        {
            var exist = InventoryCache.ContainsKey(inventoryID);
            if (exist)
            {
                var cbsItem = InventoryCache[inventoryID];
                if (cbsItem != null)
                {
                    cbsItem.SpendCount((int)spendCount);
                    OnItemUsageCountChange?.Invoke(new ItemUsesCountChange
                    {
                        ItemInventoryID = inventoryID,
                        ChangedItem = cbsItem,
                        Removed = false,
                        UsesLeft = (int)cbsItem.Count
                    });
                }
            }
        }

        internal void ConsumeSpendRequest(Dictionary<string, uint> spendDict)
        {
            foreach (var pair in spendDict)
                ConsumeSpendRequest(pair.Key, pair.Value);
        }

        protected override void OnLogout()
        {
            InventoryCache = new Dictionary<string, CBSInventoryItem>();
            LootBoxCache = new Dictionary<string, CBSLootboxInventoryItem>();
        }

        // events
        private void OnLoginSuccess(CBSLoginResult result)
        {
            if (result.IsSuccess)
            {
                if (AuthData.PreloadInventory)
                {
                    var inventory = result.Result.InfoResultPayload.UserInventory;
                    ParseInventory(inventory);
                }
            }
        }
    }
}
