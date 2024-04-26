using System.Threading.Tasks;
using CBS.Models;
using System.Collections.Generic;
using Azure.Data.Tables;
using System;

namespace CBS
{
    public class AzureChatDataProvider : IChatDataProvider
    {
        private readonly string ModeratorsTableID = "CBSChatModerators";
        private readonly string ActiveGroupChatTable = "CBSGroupChatActivity";
        private readonly string ActivePrivateChatTable = "CBSPrivateChatActivity";

        private readonly string DialogTablePrefix = "dialog";
        private readonly string ModeratorPartitionalKeyValue = "Moderator";
        private readonly string DisplayNameKey = "DisplayName";
        private readonly string RawDataKey = "RawData";
        private readonly string RowKey = "RowKey";
        private readonly string ChatTablePrefix = "Chat";
        private readonly string ChatRowKeyValue = "CBSChatMessage";
        private readonly string LastMessageIDKey = "LastMessageID";
        private readonly string InterlocutorKey = "Interlocutor";
        private readonly string DialogBadgeKey = "DialogBadge";
        private readonly string DialogMessageKey = "DialogMessage";

        public async Task<ExecuteResult<List<ProfileEntity>>> GetModeratorsAsync()
        {
            var getTableResult = await CosmosTable.GetTableAsync(ModeratorsTableID);
            if (getTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<ProfileEntity>>(getTableResult.Error);
            }
            var entityList = getTableResult.Result ?? new List<TableEntity>();
            var moderatorList = new List<ProfileEntity>();
            foreach (var entity in entityList)
            {
                moderatorList.Add(new ProfileEntity
                {
                    ProfileID = entity.RowKey,
                    DisplayName = entity.GetString(DisplayNameKey)
                });
            }

            return new ExecuteResult<List<ProfileEntity>>
            {
                Result = moderatorList
            };
        }

        public async Task<ExecuteResult<FunctionEmptyResult>> AddToModeratorsListAsync(string profileID, string displayName)
        {
            var moderatorEntity = new TableEntity();
            moderatorEntity.PartitionKey = ModeratorPartitionalKeyValue;
            moderatorEntity.RowKey = profileID;
            moderatorEntity[DisplayNameKey] = displayName;

            var addResult = await CosmosTable.AddEntityAsync(ModeratorsTableID, moderatorEntity);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(addResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public async Task<ExecuteResult<FunctionEmptyResult>> RemoveFromModeratorsListAsync(string profileID)
        {
            var deleteResult = await CosmosTable.DeleteEntityAsync(ModeratorsTableID, profileID, ModeratorPartitionalKeyValue);
            if (deleteResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(deleteResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public async Task<ExecuteResult<List<ChatMessage>>> GetMessagesFromChatAsync(string chatID, int count)
        {
            var tableID = ChatTablePrefix + chatID; 
            var getEntityResult = await StorageTable.GetTopFromTableAsync(tableID, count);
            if (getEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<ChatMessage>>(getEntityResult.Error);
            }
            var entities = getEntityResult.Result ?? new List<TableEntity>();
            var messages = new List<ChatMessage>();
            foreach (var entity in entities)
            {
                var rawData = entity.GetString(RawDataKey);
                try
                {
                    messages.Add(JsonPlugin.FromJsonDecompress<ChatMessage>(rawData));
                }
                catch{}
            }
            return new ExecuteResult<List<ChatMessage>>
            {
                Result = messages
            };
        }

        public async Task<ExecuteResult<FunctionEmptyResult>> SendMessageAsync(string chatID, ChatMessage message)
        {
            var tableID = ChatTablePrefix + chatID; 
            var partitionKey = message.MessageID;
            var rowKey = ChatRowKeyValue;
            var rawData = JsonPlugin.ToJsonCompress(message);

            var messageEntity = new TableEntity();
            messageEntity.PartitionKey = partitionKey;
            messageEntity.RowKey = rowKey;
            messageEntity[RawDataKey] = rawData;

            var addResult = await StorageTable.AddEntityAsync(tableID, messageEntity);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(addResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public async Task<ExecuteResult<FunctionEmptyResult>> UpdateChatActivityAsync(string chatID, ChatAccess access, string messageID)
        {
            var tableID = access == ChatAccess.PRIVATE ? ActivePrivateChatTable : ActiveGroupChatTable;
            var activityEntity = new TableEntity();
            activityEntity.PartitionKey = chatID;
            activityEntity.RowKey = ChatRowKeyValue;
            activityEntity[LastMessageIDKey] = messageID;
            var upsertResult = await CosmosTable.UpsertEntityAsync(tableID, activityEntity);
            if (upsertResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(upsertResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public async Task<ExecuteResult<FunctionEmptyResult>> CleanUpChatsAsync(ChatAccess access, int ttl, int saveLast)
        {
            var tableID = access == ChatAccess.PRIVATE ? ActivePrivateChatTable : ActiveGroupChatTable;
            var allEntitiesResult = await CosmosTable.GetAllEntitiesFromTableAsync(tableID);
            if (allEntitiesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(allEntitiesResult.Error);
            }
            var allEntities = allEntitiesResult.Result;
            foreach (var entity in allEntities)
            {
                var chatID = entity.PartitionKey;
                var chatTableID = ChatTablePrefix + chatID; 
                await StorageTable.RemoveOutdatedAndSaveLast(chatTableID, saveLast, ttl);
                await CosmosTable.DeleteEntityAsync(tableID, entity.RowKey, entity.PartitionKey);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public async Task<ExecuteResult<ChatMessage>> GetMessageByIDAsync(string messageID, string chatID)
        {
            var tableID = ChatTablePrefix + chatID; 
            var partitionKey = messageID;
            var rowKey = ChatRowKeyValue;

            var entityResult = await StorageTable.GetEntityAsync(tableID, partitionKey, rowKey, GetMessageKeys());
            if (entityResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatMessage>(entityResult.Error);
            }
            var messageEntity = entityResult.Result;
            var rawData = messageEntity.GetString(RawDataKey);
            var chatMessage = JsonPlugin.FromJsonDecompress<ChatMessage>(rawData);
            return new ExecuteResult<ChatMessage>
            {
                Result = chatMessage
            };
        }

        public async Task<ExecuteResult<FunctionEmptyResult>> ChangeMessageAsync(string messageID, string chatID, ChatMessage message)
        {
            var tableID = ChatTablePrefix + chatID; 
            var partitionKey = messageID;
            var rowKey = ChatRowKeyValue;
            var rawData = JsonPlugin.ToJsonCompress(message);

            var chatEntity = new TableEntity();
            chatEntity.PartitionKey = partitionKey;
            chatEntity.RowKey = rowKey;
            chatEntity[RawDataKey] = rawData;

            var updateResult = await StorageTable.UpdateEntityAsync(tableID, chatEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public async Task<ExecuteResult<ChatDialogEntry>> ClearDialogBadgeWithProfileAsync(string ownerID, string interlocutorID)
        {
            var tableID = DialogTablePrefix + ownerID;
            var partKey = interlocutorID;
            var rowKey = DialogTablePrefix;
            var getEntityResult = await StorageTable.GetEntityAsync(tableID, partKey, rowKey, GetDialogKeys());
            if (getEntityResult.Error != null || getEntityResult.Result == null)
            {
                return ErrorHandler.DialogNotFound<ChatDialogEntry>();
            }
            var entity = getEntityResult.Result;
            entity.PartitionKey = partKey;
            entity.RowKey = rowKey;
            entity[DialogBadgeKey] = 0;

            var upsertResult = await StorageTable.UpsertEntityAsync(tableID, entity);
            if (upsertResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatDialogEntry>(upsertResult.Error);
            }

            var dialogEntry = GetDialogEntryFromEntity(entity);

            return new ExecuteResult<ChatDialogEntry>
            {
                Result = dialogEntry
            };
        }

        public async Task<ExecuteResult<FunctionEmptyResult>> UpdateDialogListWithNewMessageAsync(ChatMember sender, ChatMember interlocutor, ChatMessage message)
        {
            var senderID = sender.ProfileID;
            var interlocutorID = interlocutor.ProfileID;
            var interlocutorTableID = DialogTablePrefix + interlocutorID;
            var partKey = senderID;
            var rowKey = DialogTablePrefix;

            var lastBadge = 0;
            var getEntityResult = await StorageTable.GetEntityAsync(interlocutorTableID, partKey, rowKey, GetDialogKeys());
            if (getEntityResult.Error == null && getEntityResult.Result != null)
            {
                var entity = getEntityResult.Result;
                lastBadge = entity.GetInt32(DialogBadgeKey).GetValueOrDefault();
            }
            lastBadge++;

            var profileRaw = JsonPlugin.ToJsonCompress(sender);
            var messageRaw = JsonPlugin.ToJsonCompress(message);

            var updatedEntity = new TableEntity();
            updatedEntity.PartitionKey = partKey;
            updatedEntity.RowKey = rowKey;
            updatedEntity[InterlocutorKey] = profileRaw;
            updatedEntity[DialogMessageKey] = messageRaw;
            updatedEntity[DialogBadgeKey] = lastBadge;

            var upsertResult = await StorageTable.UpsertEntityAsync(interlocutorTableID, updatedEntity);
            if (upsertResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(upsertResult.Error);
            }

            profileRaw = JsonPlugin.ToJsonCompress(interlocutor);
            var senderEntity = new TableEntity();
            senderEntity.PartitionKey = interlocutorID;
            senderEntity.RowKey = DialogTablePrefix;
            senderEntity[InterlocutorKey] = profileRaw;
            senderEntity[DialogMessageKey] = messageRaw;

            var senderTableID = DialogTablePrefix + senderID;

            upsertResult = await StorageTable.UpsertEntityAsync(senderTableID, senderEntity);
            if (upsertResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(upsertResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public async Task<ExecuteResult<FunctionGetDialogListResult>> GetDialogListAsync(string profileID, int count)
        {
            var tableID = DialogTablePrefix + profileID;
            var getResult = await StorageTable.GetTopFromTableAndSaveLastNAsync(tableID, count);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetDialogListResult>(getResult.Error);
            }

            var entities = getResult.Result ?? new List<TableEntity>();
            var dialogList = new List<ChatDialogEntry>();
            foreach (var entity in entities)
            {
                try
                {
                    dialogList.Add(GetDialogEntryFromEntity(entity));
                }
                catch {}
            }

            return new ExecuteResult<FunctionGetDialogListResult>
            {
                Result = new FunctionGetDialogListResult
                {
                    DialogList = dialogList
                }
            };
        }

        private string [] GetModeratorsKeys()
        {
            return new string [] 
            {
                RowKey,
                DisplayNameKey
            };
        }

        private string [] GetMessageKeys()
        {
            return new string [] 
            {
                RowKey,
                RawDataKey
            };
        }

        private string [] GetDialogKeys()
        {
            return new string []
            {
                InterlocutorKey,
                DialogBadgeKey,
                DialogMessageKey
            };
        }

        public string GenerateNextMessageID()
        {
            return (DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks).ToString("d19");
        }

        private ChatDialogEntry GetDialogEntryFromEntity(TableEntity entity)
        {
            var interlocutorRaw = entity.GetString(InterlocutorKey);
            var messageRaw = entity.GetString(DialogMessageKey);
            var badgeCount = entity.GetInt32(DialogBadgeKey).GetValueOrDefault();

            var interlocutor = JsonPlugin.FromJsonDecompress<ChatMember>(interlocutorRaw);
            var message = JsonPlugin.FromJsonDecompress<ChatMessage>(messageRaw);

            return new ChatDialogEntry
            {
                InterlocutorProfile = interlocutor,
                LastMessage = message,
                BadgeCount = badgeCount
            };
        }
    }
}