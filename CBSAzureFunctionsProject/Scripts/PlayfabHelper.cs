using PlayFab.Samples;
using CBS.Models;
using System.Collections.Generic;
using Newtonsoft.Json;
using PlayFab.ClientModels;
using System.Linq;

namespace CBS
{
    public static class PlayfabHelper
    {
        public const string ProfileIDArgsKey = "ProfileID";
        public const string ClanIDArgsKey = "ClanID";
        public const int MaxInventoryRevokeCountByRequest = 25;

        public static T GetRequest<T>(this FunctionExecutionContext<dynamic> context) where T : FunctionBaseRequest
        {
            var args = context.FunctionArgument;
            var rawData = args == null ? JsonConvert.SerializeObject(new object{}) : JsonConvert.SerializeObject(args);
            return JsonConvert.DeserializeObject<T>(rawData);
        }

        public static PlayFab.ClientModels.ItemInstance ToClientInstance(this PlayFab.ServerModels.ItemInstance item)
        {
            var rawData = JsonConvert.SerializeObject(item);
            return JsonConvert.DeserializeObject<PlayFab.ClientModels.ItemInstance>(rawData);
        }

        public static PlayFab.ClientModels.CatalogItem ToClientInstance(this PlayFab.ServerModels.CatalogItem item)
        {
            var rawData = JsonConvert.SerializeObject(item);
            return JsonConvert.DeserializeObject<PlayFab.ClientModels.CatalogItem>(rawData);
        }

        public static string AsFunctionResult(this object result)
        {
            var json = JsonConvert.SerializeObject(result, Formatting.None, new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            });
            return Compressor.Compress(json);
        }

        public static PlayFab.ClientModels.GetCatalogItemsResult ToClientInstance(this PlayFab.ServerModels.GetCatalogItemsResult result)
        {
            var rawData = JsonConvert.SerializeObject(result);
            return JsonConvert.DeserializeObject<PlayFab.ClientModels.GetCatalogItemsResult>(rawData);
        }

        public static PlayFab.ServerModels.ItemInstance ToServerInstance(this PlayFab.ClientModels.ItemInstance result)
        {
            var rawData = JsonConvert.SerializeObject(result);
            return JsonConvert.DeserializeObject<PlayFab.ServerModels.ItemInstance>(rawData);
        }

        public static List<ItemInstance> ToClientInstances(this List<PlayFab.ServerModels.GrantedItemInstance> result)
        {
            var rawData = JsonConvert.SerializeObject(result);
            return JsonConvert.DeserializeObject<List<ItemInstance>>(rawData);
        }

        public static List<ItemInstance> ToClientInstances(this List<PlayFab.ServerModels.ItemInstance> result)
        {
            var rawData = JsonConvert.SerializeObject(result);
            return JsonConvert.DeserializeObject<List<ItemInstance>>(rawData);
        }

        public static BanDetail ToEntityBan(this PlayFab.ServerModels.BanInfo banInfo)
        {
            return new BanDetail
            {
                ProfileID = banInfo.PlayFabId,
                BanId = banInfo.BanId,
                Expires = banInfo.Expires,
                Reason = banInfo.Reason,
                Active = banInfo.Active,
                Created = banInfo.Created
            };
        }

        public static bool IsConsumable(this PlayFab.ServerModels.CatalogItem item)
        {
            if (item.Consumable == null)
                return false;
            return item.Consumable.UsageCount > 0;
        }

        public static bool IsItem(this PlayFab.ServerModels.CatalogItem item)
        {
            return item.Container == null && item.Bundle == null;
        }

        public static bool IsPack(this PlayFab.ServerModels.CatalogItem item)
        {
            return item.Bundle != null;
        }

        public static bool IsLootbox(this PlayFab.ServerModels.CatalogItem item)
        {
            return item.Container != null;
        }

        public static bool IsLootbox(this PlayFab.ServerModels.ItemInstance item)
        {
            return item.BundleContents != null;
        }

        public static string GetCategory(this PlayFab.ServerModels.CatalogItem item)
        {
            bool tagExist = item.Tags != null && item.Tags.Count != 0;
            var category = tagExist ? item.Tags[0] : string.Empty;
            return category;
        }

        public static string GetTag(this PlayFab.ServerModels.FriendInfo friend)
        {
            var tags = friend.Tags;
            if (tags == null || tags.Count == 0)
                return string.Empty;
            return tags.FirstOrDefault();
        }

        public static PlayFab.GroupsModels.EntityKey ToGroupEntity(this PlayFab.AdminModels.EntityKey entity)
        {
            return new PlayFab.GroupsModels.EntityKey
            {
                Id = entity.Id,
                Type = entity.Type
            };
        }

        public static PlayFab.DataModels.EntityKey ToDataEntity(this PlayFab.GroupsModels.EntityKey entity)
        {
            return new PlayFab.DataModels.EntityKey
            {
                Id = entity.Id,
                Type = entity.Type
            };
        }
    }
}