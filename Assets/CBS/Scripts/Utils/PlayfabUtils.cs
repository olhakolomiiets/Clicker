using CBS.Models;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.MultiplayerModels;
using System.Collections.Generic;
using System.Linq;
using CBS.SharedData.Lootbox;

namespace CBS.Utils
{
    public static class PlayfabUtils
    {
        public const int DEFAULT_CUSTOM_DATA_SIZE = 10000;
        public const int ITEM_CUSTOM_DATA_SIZE = 1000;
        public const int TITLE_DATA_SIZE = 1000000;

        public const string REAL_MONEY_CODE = "RM";

        public static string ToJson(this PlayFabAuthenticationContext context)
        {
            return JsonPlugin.ToJson(context);
        }

        public static string ToJson(this MatchmakingQueueConfig context)
        {
            return JsonPlugin.ToJson(context);
        }

        public static Dictionary<string, CBSCurrency> GetCBSCurrencies(this CatalogItemBundleInfo bundle)
        {
            return bundle.BundledVirtualCurrencies.Select(x => CBSCurrency.Create(x.Key, x.Value)).ToDictionary(x => x.Code, x => x);
        }

        public static string GetRMPriceString(this CBSPrice price)
        {
            if (price.CurrencyID == REAL_MONEY_CODE)
            {
                var rmValue = price.CurrencyValue;
                var rmFloat = (float)rmValue / 100f;
                return rmFloat.ToString("0.00") + "$";
            }
            else
            {
                return string.Empty;
            }
        }

#if UNITY_EDITOR && ENABLE_PLAYFABADMIN_API
        public static PlayFab.AdminModels.GetStoreItemsResult Copy(this PlayFab.AdminModels.GetStoreItemsResult result)
        {
            return JsonPlugin.FromJson<PlayFab.AdminModels.GetStoreItemsResult>(JsonPlugin.ToJson(result));
        }

        public static string GetRMPriceString(this PlayFab.AdminModels.CatalogItem item)
        {
            var prices = item.VirtualCurrencyPrices ?? new Dictionary<string, uint>();
            if (prices.ContainsKey(REAL_MONEY_CODE))
            {
                var rmValue = prices[REAL_MONEY_CODE];
                var rmFloat = (float)rmValue / 100f;
                return rmFloat.ToString("0.00") + "$";
            }
            else
            {
                return string.Empty;
            }
        }

        public static string GetRMPriceString(this PlayFab.AdminModels.StoreItem item)
        {
            var prices = item.VirtualCurrencyPrices ?? new Dictionary<string, uint>();
            if (prices.ContainsKey(REAL_MONEY_CODE))
            {
                var rmValue = prices[REAL_MONEY_CODE];
                var rmFloat = (float)rmValue / 100f;
                return rmFloat.ToString("0.00") + "$";
            }
            else
            {
                return string.Empty;
            }
        }

        public static string GetRMOverridePriceString(this PlayFab.AdminModels.StoreItem item)
        {
            var prices = item.VirtualCurrencyPrices ?? new Dictionary<string, uint>();
            if (prices.ContainsKey(REAL_MONEY_CODE))
            {
                var rmValue = prices[REAL_MONEY_CODE];
                var rmFloat = (float)rmValue / 100f;
                return rmFloat.ToString("0.00") + "$";
            }
            else
            {
                return string.Empty;
            }
        }
#endif

        public static string GetRMPriceString(this PlayFab.ClientModels.CatalogItem item)
        {
            var prices = item.VirtualCurrencyPrices ?? new Dictionary<string, uint>();
            if (prices.ContainsKey(REAL_MONEY_CODE))
            {
                var rmValue = prices[REAL_MONEY_CODE];
                var rmFloat = (float)rmValue / 100f;
                return rmFloat.ToString("0.00") + "$";
            }
            else
            {
                return string.Empty;
            }
        }

        public static string CurrencyValueToString(string currencyCode, int value)
        {
            if (currencyCode == REAL_MONEY_CODE)
            {
                var rmFloat = (float)value / 100f;
                return rmFloat.ToString("0.00") + "$";
            }
            else
            {
                return value.ToString();
            }
        }

        public static bool IsConsumable(this CatalogItem item)
        {
            if (item.Consumable == null)
                return false;
            return item.Consumable.UsageCount > 0;
        }

        public static bool IsItem(this CatalogItem item)
        {
            return item.Container == null && item.Bundle == null;
        }

        public static bool IsPack(this CatalogItem item)
        {
            return item.Bundle != null;
        }

        public static bool IsLootbox(this CatalogItem item)
        {
            return item.Container != null;
        }

        public static string GetCategory(this CatalogItem item)
        {
            bool tagExist = item.Tags != null && item.Tags.Count != 0;
            var category = tagExist ? item.Tags[0] : string.Empty;
            return category;
        }

        public static CBSItem ToCBSItem(this CatalogItem item, CBSItemRecipe recipe, List<CBSItemUpgradeState> upgradeState)
        {
            return new CBSItem(item, recipe, upgradeState);
        }

        public static CBSItemPack ToCBSPack(this CatalogItem item)
        {
            return new CBSItemPack(item);
        }

        public static CBSLootbox ToCBSLootbox(this CatalogItem item, CBSLootboxTable lootboxTable)
        {
            return new CBSLootbox(item, lootboxTable);
        }

        public static CBSInventoryItem ToCBSInventoryItem(this ItemInstance instance)
        {
            var itemID = instance.ItemId;
            var cbsItems = CBSModule.Get<CBSItemsModule>();
            var cbsItem = cbsItems.GetFromCache(itemID);
            return new CBSInventoryItem(instance, cbsItem);
        }
        
        public static CBSLootboxInventoryItem ToCBSLootboxInventoryItem(this ItemInstance instance)
        {
            var itemID = instance.ItemId;
            var cbsItems = CBSModule.Get<CBSItemsModule>();
            var cbsItem = cbsItems.GetFromCache(itemID);
            return new CBSLootboxInventoryItem(instance, cbsItem);
        }
        
        public static CBSLootboxInventoryItem ToCBSLootboxInventoryItem(this CBSInventoryItem instance)
        {
            var itemID = instance.ItemID;
            var cbsItems = CBSModule.Get<CBSItemsModule>();
            var cbsItem = cbsItems.GetFromCache(itemID);
            return new CBSLootboxInventoryItem(instance.PlayFabInstance, cbsItem);
        }
    }
}
