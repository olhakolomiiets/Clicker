using CBS.Models;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CBS
{
    public class CBSInventoryItem : CBSBaseItem
    {
        public string InstanceID { get; private set; }
        public bool IsEquippable { get; private set; }
        public bool IsConsumable { get; private set; }
        public bool IsStackable { get; private set; }
        public bool IsTradable { get; private set; }
        public bool IsRecipe { get; private set; }
        public bool IsUpgradable { get; private set; }
        public bool HasLifeTime { get; private set; }
        public DateTime? Expiration { get; private set; }
        public DateTime? PurchaseDate { get; private set; }
        public int? Count { get; private set; }
        public int PurchasePrice { get; private set; }
        public string PurchaseCurrency { get; private set; }
        public PurchaseType PurchaseAction { get; private set; }
        public int UpgradeIndex { get; private set; }
        public bool Equipped { get; private set; }
        public ItemInstance PlayFabInstance { get; private set; }

        internal bool IsInTrading { get; private set; }

        private CBSBaseItem BaseItem { get; set; }
        private Dictionary<string, string> InventoryData;

        public CBSInventoryItem(ItemInstance inventoryItem, CBSBaseItem baseItem)
        {
            PlayFabInstance = inventoryItem;
            BaseItem = baseItem;
            ItemID = inventoryItem.ItemId;
            InstanceID = inventoryItem.ItemInstanceId;
            DisplayName = inventoryItem.DisplayName;
            ItemClass = inventoryItem.ItemClass;
            Expiration = inventoryItem.Expiration;
            PurchaseDate = inventoryItem.PurchaseDate;
            Count = inventoryItem.RemainingUses;
            PurchasePrice = (int)inventoryItem.UnitPrice;
            PurchaseCurrency = inventoryItem.UnitCurrency;
            PurchaseAction = PurchasePrice == 0 && string.IsNullOrEmpty(PurchaseCurrency) ? PurchaseType.GRANTED : PurchaseType.PURCHASED;
            HasLifeTime = Expiration != null;
            InventoryData = inventoryItem.CustomData;

            bool hasEquipData = inventoryItem.CustomData != null && inventoryItem.CustomData.Count > 0 && inventoryItem.CustomData.ContainsKey(ItemDataKeys.InventoryEquippedKey);
            bool hasTradeData = inventoryItem.CustomData != null && inventoryItem.CustomData.Count > 0 && inventoryItem.CustomData.ContainsKey(ItemDataKeys.InventoryTradeKey);
            bool hasBaseData = inventoryItem.CustomData != null && inventoryItem.CustomData.Count > 0 && inventoryItem.CustomData.ContainsKey(ItemDataKeys.InventoryBaseDataKey);
            bool hasIndexData = inventoryItem.CustomData != null && inventoryItem.CustomData.Count > 0 && inventoryItem.CustomData.ContainsKey(ItemDataKeys.UpgradeIndexKey);

            if (baseItem != null)
            {
                Category = baseItem.Category;
                Description = baseItem.Description;
                ExternalIconURL = baseItem.ExternalIconURL;
                Prices = baseItem.Prices;
                CustomData = baseItem.CustomData;
                ItemClass = baseItem.ItemClass;
            }

            var baseData = GetCustomData<CBSItemCustomData>();
            IsEquippable = baseData == null ? false : baseData.IsEquippable;
            IsRecipe = baseData == null ? false : baseData.IsRecipe;
            Type = baseData == null ? ItemType.ITEMS : baseData.ItemType;
            IsConsumable = baseData == null ? false : baseData.IsConsumable;
            IsStackable = baseData == null ? false : baseData.IsStackable;
            IsTradable = baseData == null ? false : baseData.IsTradable;
            IsUpgradable = GetUpgradeList() != null && GetUpgradeList().Count > 0;

            Equipped = hasEquipData ? bool.Parse(inventoryItem.CustomData[ItemDataKeys.InventoryEquippedKey]) : false;
            IsInTrading = hasTradeData ? bool.Parse(inventoryItem.CustomData[ItemDataKeys.InventoryTradeKey]) : false;
            UpgradeIndex = hasIndexData ? int.Parse(inventoryItem.CustomData[ItemDataKeys.UpgradeIndexKey]) : 0;
        }

        public Dictionary<string, string> GetInventoryData()
        {
            return InventoryData == null ? new Dictionary<string, string>() : InventoryData;
        }

        public string GetInventoryDataByKey(string key)
        {
            var rawData = InventoryData == null || !InventoryData.ContainsKey(key) ? string.Empty : InventoryData[key];
            return rawData;
        }

        public override T GetCustomData<T>()
        {
            return BaseItem == null ? null : BaseItem.GetCustomData<T>();
        }

        public Dictionary<string, object> GetInventoryDataAsDictionary<T>(string key) where T : class
        {
            var inventoryRawData = GetInventoryDataByKey(key);
            if (string.IsNullOrEmpty(inventoryRawData))
                return null;
            var type = typeof(T);
            var data = JsonUtility.FromJson(inventoryRawData, type);
            var baseList = typeof(CBSItemCustomData).GetFields().Where(f => f.IsPublic).Select(x => x.Name).ToList();
            var list = type.GetFields().Where(f => f.IsPublic && !baseList.Contains(f.Name));
            return list.ToDictionary(x => x.Name, x => x.GetValue(data));
        }

        public override CBSItemRecipe GetRecipeData()
        {
            return BaseItem?.GetRecipeData();
        }

        public override List<CBSItemUpgradeState> GetUpgradeList()
        {
            return BaseItem?.GetUpgradeList();
        }

        public bool IsMaxUpgrade()
        {
            if (!IsUpgradable)
            {
                return false;
            }
            return UpgradeIndex >= GetUpgradeList().Count - 1;
        }

        public CBSItemUpgradeState GetCurrentUpgradeData()
        {
            if (!IsUpgradable)
            {
                return null;
            }
            return IsMaxUpgrade() ? GetUpgradeList().LastOrDefault() : GetUpgradeList().ElementAt(UpgradeIndex);
        }

        public CBSItemUpgradeState GetNextUpgradeData()
        {
            if (!IsUpgradable)
            {
                return null;
            }
            return IsMaxUpgrade() ? null : GetUpgradeList().ElementAt(UpgradeIndex + 1);
        }

        public T GetCurrentUpgradeCustomData<T>() where T : CBSUpgradeItemCustomData
        {
            var state = GetCurrentUpgradeData();
            if (state == null)
            {
                return null;
            }
            return state.GetCustomData<T>();
        }

        public Dictionary<string, object> GetCurrentUpgradeCustomDataAsDictionary()
        {
            var state = GetCurrentUpgradeData();
            if (state == null)
            {
                return null;
            }
            var dataClass = state.CustomDataClassName;
            var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()).FirstOrDefault(x => x.Name == dataClass);
            var data = new object();
            try
            {
                data = JsonPlugin.FromJsonDecompress(state.CustomRawData, type);
            }
            catch
            {
                data = JsonPlugin.FromJson(state.CustomRawData, type);
            }
            var list = type.GetFields().Where(f => f.IsPublic);
            return list.ToDictionary(x => x.Name, x => x.GetValue(data));
        }

        public T GetNextUpgradeCustomData<T>() where T : CBSUpgradeItemCustomData
        {
            var state = GetNextUpgradeData();
            if (state == null)
            {
                return null;
            }
            return state.GetCustomData<T>();
        }

        public Dictionary<string, object> GetNextUpgradeCustomDataAsDictionary()
        {
            var state = GetNextUpgradeData();
            if (state == null)
            {
                return null;
            }
            var dataClass = state.CustomDataClassName;
            var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()).FirstOrDefault(x => x.Name == dataClass);
            var data = new object();
            try
            {
                data = JsonPlugin.FromJsonDecompress(state.CustomRawData, type);
            }
            catch
            {
                data = JsonPlugin.FromJson(state.CustomRawData, type);
            }
            var list = type.GetFields().Where(f => f.IsPublic);
            return list.ToDictionary(x => x.Name, x => x.GetValue(data));
        }


        // internal
        internal void SetUsageCount(int count)
        {
            Count = count;
        }

        internal void SpendCount(int count)
        {
            if (count > Count)
                return;
            Count -= count;
        }
    }
}