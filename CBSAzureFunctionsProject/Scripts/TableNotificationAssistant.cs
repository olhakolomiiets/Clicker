using Azure.Data.Tables;
using CBS.Models;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace CBS
{
    public class TableNotificationAssistant
    {
        private static readonly string NotificationTableID = "CBSNotificationTable";
        private static readonly string ProfileTablePrefix = "notification";

        private static readonly string RawDataKey = "RawData";
        private static readonly string RowKey = "RowKey";
        private static readonly string PartitionKey = "PartitionKey";
        private static readonly string ReadStateKey = "Read";
        private static readonly string RewardStateKey = "Rewarded";

        public static async Task<ExecuteResult<List<CBSNotification>>> GetGlobalNotificationsAsync(int maxCount, int ttl)
        {
            var entityResult = await StorageTable.GetTopFromTableAsync(NotificationTableID, maxCount, ttl: ttl);
            if (entityResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<CBSNotification>>(entityResult.Error);
            }
            var entities = entityResult.Result ?? new List<TableEntity>();
            var notifications = ParseNotificationFromEntities(entities);

            return new ExecuteResult<List<CBSNotification>>
            {
                Result = notifications
            };
        }

        public static async Task<ExecuteResult<List<CBSNotification>>> GetProfileNotificationsAsync(string profileID, int maxCount, long? lastUpdate, int ttl)
        {
            var sBuilder = new StringBuilder(ProfileTablePrefix);
            sBuilder.Append(profileID);
            var tableID = sBuilder.ToString();

            var topEntityResult = await StorageTable.GetFirstEntityAsync(NotificationTableID, ttl: ttl);
            if (topEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<CBSNotification>>(topEntityResult.Error);
            }
            var topEntityExist = topEntityResult.Result != null;
            if (topEntityExist)
            {
                var topEntity = topEntityResult.Result;
                var lastNotificationTimestampe = topEntity.Timestamp;
                var lastNotificationDate = lastNotificationTimestampe.GetValueOrDefault().UtcDateTime;
                var checkUpdate = lastUpdate == null || lastNotificationDate.Ticks > (long)lastUpdate;
                if (checkUpdate)
                {
                    var getGlobalNotificationResult = await StorageTable.GetTopFromTableAsync(NotificationTableID, maxCount, ttl: ttl);
                    if (getGlobalNotificationResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<List<CBSNotification>>(getGlobalNotificationResult.Error);
                    }
                    var globalNotifications = getGlobalNotificationResult.Result ?? new List<TableEntity>();
                    var entitiesToUpdate = lastUpdate == null ? globalNotifications : globalNotifications.Where(x=>x.Timestamp.GetValueOrDefault().UtcDateTime.Ticks > (long)lastUpdate);

                    var addEntityTaskList = new List<Task<ExecuteResult<Azure.Response>>>();
                    foreach (var entity in entitiesToUpdate)
                    {
                        var addTask = StorageTable.AddEntityAsync(tableID, entity, ttl: ttl);
                        addEntityTaskList.Add(addTask);
                    }
                    await Task.WhenAll(addEntityTaskList);

                    // save last update
                    var saveResult = await NotificationModule.SaveLastUpdateAsync(profileID, lastNotificationDate);
                    if (saveResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<List<CBSNotification>>(saveResult.Error);
                    }
                }
            }

            var entityResult = await StorageTable.GetTopFromTableAndDeleteOutDatesAsync(tableID, maxCount, ttl: ttl);
            if (entityResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<CBSNotification>>(entityResult.Error);
            }
            var entities = entityResult.Result ?? new List<TableEntity>();
            var notifications = ParseNotificationFromEntities(entities);

            return new ExecuteResult<List<CBSNotification>>
            {
                Result = notifications
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SendGlobalNotificationAsync(CBSNotification notification, int ttl)
        {
            var sendResult = await SendNotificationAsync(NotificationTableID, notification, ttl);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(sendResult.Error);
            }
            var checkOutDatedResult = await StorageTable.GetTopFromTableAndDeleteOutDatesAsync(NotificationTableID, NotificationsData.MAX_NOTIFICATIONS_LENGTH, ttl);
            if (checkOutDatedResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(checkOutDatedResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SendProfileNotificationAsync(string profileID, CBSNotification notification, int ttl)
        {
            var sBuilder = new StringBuilder(ProfileTablePrefix);
            sBuilder.Append(profileID);
            var tableID = sBuilder.ToString();
            var sendResult = await SendNotificationAsync(tableID, notification, ttl);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(sendResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SendNotificationAsync(string tableID, CBSNotification notification, int ttl)
        {
            var partitionKey = TicksKey();
            notification.InstanceID = partitionKey;
            var notificationEntity = GetEntityFromNotification(partitionKey, notification);
            var addResult = await StorageTable.AddEntityAsync(tableID, notificationEntity, ttl: ttl);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(addResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<CBSNotification>> GetProfileNotificationByInstanceIDAsync(string profileID, string notificationInstanceID)
        {
            var sBuilder = new StringBuilder(ProfileTablePrefix);
            sBuilder.Append(profileID);
            var tableID = sBuilder.ToString();

            var getEntityResult = await StorageTable.GetEntityAsync(tableID, notificationInstanceID, notificationInstanceID, GetNotificationEntityKeys());
            if (getEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<CBSNotification>(getEntityResult.Error);
            }

            var notificationEntity = getEntityResult.Result;
            var notification = ParseNotificationFromEntity(notificationEntity);

            return new ExecuteResult<CBSNotification>
            {
                Result = notification
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> MarkNotificationAsReadAsync(string profileID, string notificationInstanceID)
        {
            var sBuilder = new StringBuilder(ProfileTablePrefix);
            sBuilder.Append(profileID);
            var tableID = sBuilder.ToString();

            var updateEntity = new TableEntity();
            updateEntity.PartitionKey = notificationInstanceID;
            updateEntity.RowKey = notificationInstanceID;
            updateEntity[ReadStateKey] = true;

            var upsertResult = await StorageTable.UpsertEntityAsync(tableID, updateEntity);
            if (upsertResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(upsertResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> MarkNotificationAsRewardedAsync(string profileID, string notificationInstanceID)
        {
            var sBuilder = new StringBuilder(ProfileTablePrefix);
            sBuilder.Append(profileID);
            var tableID = sBuilder.ToString();

            var updateEntity = new TableEntity();
            updateEntity.PartitionKey = notificationInstanceID;
            updateEntity.RowKey = notificationInstanceID;
            updateEntity[RewardStateKey] = true;

            var upsertResult = await StorageTable.UpsertEntityAsync(tableID, updateEntity);
            if (upsertResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(upsertResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> RemoveNotificationAsync(string profileID, string notificationInstanceID)
        {
            var sBuilder = new StringBuilder(ProfileTablePrefix);
            sBuilder.Append(profileID);
            var tableID = sBuilder.ToString();

            var removeResult = await StorageTable.DeleteEntityAsync(tableID, notificationInstanceID, notificationInstanceID);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(removeResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult{}
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> RemoveProfileEntryAsync(string profileID)
        {
            var sBuilder = new StringBuilder(ProfileTablePrefix);
            sBuilder.Append(profileID);
            var tableID = sBuilder.ToString();
            var deleteResult = await CosmosTable.DeleteTableAsync(tableID);
            if (deleteResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(deleteResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = deleteResult.Result
            };
        }

        // internal
        private static string [] GetNotificationEntityKeys()
        {
            return new string [] 
            {
                RowKey,
                PartitionKey,
                RawDataKey,
                ReadStateKey,
                RewardStateKey
            };
        }

        private static List<CBSNotification> ParseNotificationFromEntities(List<TableEntity> entities)
        {
            var notifications = new List<CBSNotification>();
            foreach (var entity in entities)
            {
                var notification = ParseNotificationFromEntity(entity);

                if (notification != null)
                {
                    notifications.Add(notification);
                }
            }
            notifications.Sort((x, y) => DateTime.Compare(x.CreatedDate, y.CreatedDate));
            notifications.Reverse();
            return notifications;
        }

        private static CBSNotification ParseNotificationFromEntity(TableEntity entity)
        {
            var instanceID = entity.PartitionKey;
            var rawData = entity.GetString(RawDataKey);
            var readState = entity.GetBoolean(ReadStateKey).GetValueOrDefault();
            var rewardedState = entity.GetBoolean(RewardStateKey).GetValueOrDefault();
            try
            {
                var notification = JsonPlugin.FromJsonDecompress<CBSNotification>(rawData);
                notification.Read = readState;
                notification.Rewarded = rewardedState;
                notification.InstanceID = instanceID;

                return notification;
            }
            catch {
                return null;
            }
        }

        private static TableEntity GetEntityFromNotification(string partitionKey, CBSNotification notification)
        {
            var rawData = JsonPlugin.ToJsonCompress(notification);
            var entity = new TableEntity
            {
                RowKey = partitionKey,
                PartitionKey = partitionKey
            };
            entity[RawDataKey] = rawData;
            return entity;
        }

        public static string TicksKey()
        {
            return (DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks).ToString("d19");
        }

        public static DateTimeOffset TicksKeyToDateTimeOffset(string ticksKey)
        {
            return new DateTimeOffset(DateTime.MaxValue.Ticks - long.Parse(ticksKey), TimeSpan.Zero);
        }
    }
}