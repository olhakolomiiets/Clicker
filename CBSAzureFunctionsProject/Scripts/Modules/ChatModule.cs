using PlayFab.ServerModels;
using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using Censored;
using Medallion.Threading.Azure;
using Medallion.Threading;

namespace CBS
{
    public class ChatModule : BaseAzureModule
    {
        private static readonly int MaxMessagesPerRequest = 1000;
        private static readonly int MaxDialogListCountPerProfile = 100;
        private static bool IsInited;
        private static IChatDataProvider ChatDataProvider;
        private static ChatConfigData ChatConfig;
        private static Censor ChatCensor;


        [FunctionName(AzureFunctions.GetModeratorsListMethod)]
        public static async Task<dynamic> GetModeratorsListTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var getResult = await GetModeratorsListAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddToModeratorListMethod)]
        public static async Task<dynamic> AddToModeratorListTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionModifyModeratorRequest>();

            var profileID = request.ProfileID;
            var displayName = request.Nickname;

            var addResult = await AddProfileToModeratorListAsync(profileID, displayName);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError(addResult.Error).AsFunctionResult();
            }

            return addResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.RemoveFromModeratorListMethod)]
        public static async Task<dynamic> RemoveFromModeratorListTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionModifyModeratorRequest>();

            var profileID = request.ProfileID;
            var displayName = request.Nickname;

            var removeResult = await RemoveProfileFromModeratorListAsync(profileID, displayName);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError(removeResult.Error).AsFunctionResult();
            }

            return removeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetMessagesFromChatMethod)]
        public static async Task<dynamic> GetMessagesFromChatTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<GetChatMessagesRequest>();

            var profileID = request.ProfileID;
            var chatID = request.ChatID;
            var count = request.Count;

            var getResult = await GetMessagesFromChatAsync(chatID, count);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SendMessageToChatMethod)]
        public static async Task<dynamic> SendMessageToChatTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionSendChatMessageRequest>();

            var sendResult = await SendProfileMessageToChatAsync(request);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError(sendResult.Error).AsFunctionResult();
            }

            return sendResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ModifyChatMessageMethod)]
        public static async Task<dynamic> ModifiyChatMessageTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionModifyChatMessageRequest>();

            var profileID = request.ProfileID;
            var messageID = request.MessageID;
            var chatID = request.ChatID;
            var textToEdit = request.TextToEdit;

            var changeResult = await ModifyMessageTextAsync(profileID, messageID, chatID, textToEdit);
            if (changeResult.Error != null)
            {
                return ErrorHandler.ThrowError(changeResult.Error).AsFunctionResult();
            }

            return changeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.DeleteChatMessageMethod)]
        public static async Task<dynamic> DeleteChatMessageTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionChatMessageRequest>();

            var profileID = request.ProfileID;
            var messageID = request.MessageID;
            var chatID = request.ChatID;

            var deleteResult = await DeleteMessageAsync(profileID, messageID, chatID);
            if (deleteResult.Error != null)
            {
                return ErrorHandler.ThrowError(deleteResult.Error).AsFunctionResult();
            }

            return deleteResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.BanProfileInChatMethod)]
        public static async Task<dynamic> BanProfileInChatTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<ChatBanRequest>();

            var moderatorID = request.ProfileID;
            var chatID = request.ChatID;
            var profileIDForBan = request.ProfileIDForBan;
            var reason = request.Reason;
            var banHours = request.BanHours;

            var banResult = await BanProfileInChatAsync(moderatorID, profileIDForBan, chatID, banHours, reason);
            if (banResult.Error != null)
            {
                return ErrorHandler.ThrowError(banResult.Error).AsFunctionResult();
            }

            return banResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetChatStickersPackMethod)]
        public static async Task<dynamic> GetChatStickersPackTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var getResult = await GetStickersPackAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ClaimItemFormMessageMethod)]
        public static async Task<dynamic> ClaimItemFormMessageTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionChatMessageRequest>();
            var chatID = request.ChatID;
            var messageID = request.MessageID;
            var profileID = request.ProfileID;

            var claimResult = await ClaimItemFromChatAsync(profileID, chatID, messageID);
            if (claimResult.Error != null)
            {
                return ErrorHandler.ThrowError(claimResult.Error).AsFunctionResult();
            }

            return claimResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ClearDialogBadgeMethod)]
        public static async Task<dynamic> ClearDialogBadgeMethodTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();

            var profileID = request.ProfileID;
            var withProfileID = request.ID;

            var clearResult = await ClearDialogBadgeEntryAsync(profileID, withProfileID);
            if (clearResult.Error != null)
            {
                return ErrorHandler.ThrowError(clearResult.Error).AsFunctionResult();
            }

            return clearResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetDialogListMethod)]
        public static async Task<dynamic> GetDialogListTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var profileID = request.ProfileID;

            var getResult = await GetProfileDialogListAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetDialogBadgeMethod)]
        public static async Task<dynamic> GetDialogBadgeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var profileID = request.ProfileID;

            var getResult = await GetDialogListBadgeAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureTimers.ChatCleanTimer)]
        public static async Task ChatCleanTimerTrigger([TimerTrigger(CronExpression.EVERY_DAY_AT_12_00_AM)]TimerInfo myTimer, ILogger log)
        {
            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error == null)
            {
                var dataProvider = dataProviderResult.Result;

                var chatConfigResult = await GetChatConfigDataAsync();
                if (chatConfigResult.Error == null)
                {
                    var chatConfig = chatConfigResult.Result;

                    var privateChatTTL = chatConfig.GetPrivateTTL();
                    var groupChatTTL = chatConfig.GetGeneralTTL();

                    if (privateChatTTL > 0)
                    {
                        await dataProvider.CleanUpChatsAsync(ChatAccess.PRIVATE, privateChatTTL, chatConfig.GetDontRemoveCount(ChatAccess.PRIVATE));
                    }
                    if (groupChatTTL > 0)
                    {
                        await dataProvider.CleanUpChatsAsync(ChatAccess.GROUP, groupChatTTL, chatConfig.GetDontRemoveCount(ChatAccess.GROUP));
                    }
                }
            }  
        }

        public static async Task<ExecuteResult<FunctionModeratorListResult>> GetModeratorsListAsync()
        {
            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModeratorListResult>(dataProviderResult.Error);
            }
            var dataProvider = dataProviderResult.Result;
            var dataResult = await dataProvider.GetModeratorsAsync();
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModeratorListResult>(dataResult.Error);
            }
            var moderators = dataResult.Result;

            return new ExecuteResult<FunctionModeratorListResult>
            {
                Result = new FunctionModeratorListResult
                {
                    Moderators = moderators
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> AddProfileToModeratorListAsync(string profileID, string displayName)
        {
            var profileResult = await ProfileModule.GetProfileAccountInfoByIdOrName(profileID, displayName);
            if (profileResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(profileResult.Error);
            }
            var profile = profileResult.Result;
            var id = profile.PlayFabId;
            var nickname = profile.TitleInfo.DisplayName;

            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(dataProviderResult.Error);
            }
            var dataProvider = dataProviderResult.Result;
            var addResult = await dataProvider.AddToModeratorsListAsync(id, nickname);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(addResult.Error);
            }

            var chatDataResult = await GetProfileChatDataAsync(id);
            if (chatDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(chatDataResult.Error);
            }
            var chatData = chatDataResult.Result;
            chatData.IsModerator = true;

            var saveDataResult = await SaveProfileChatDataAsync(id, chatData);
            if (saveDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveDataResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> RemoveProfileFromModeratorListAsync(string profileID, string displayName)
        {
            var profileResult = await ProfileModule.GetProfileAccountInfoByIdOrName(profileID, displayName);
            if (profileResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(profileResult.Error);
            }
            var profile = profileResult.Result;
            var id = profile.PlayFabId;

            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(dataProviderResult.Error);
            }

            var dataProvider = dataProviderResult.Result;
            var removeResult = await dataProvider.RemoveFromModeratorsListAsync(id);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(removeResult.Error);
            }

            var chatDataResult = await GetProfileChatDataAsync(id);
            if (chatDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(chatDataResult.Error);
            }
            var chatData = chatDataResult.Result;
            chatData.IsModerator = false;

            var saveDataResult = await SaveProfileChatDataAsync(id, chatData);
            if (saveDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveDataResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<ProfileChatData>> GetProfileChatDataAsync(string profileID)
        {
            var getResult = await GetProfileInternalDataAsObject<ProfileChatData>(profileID, ProfileDataKeys.ProfileChatData);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError<ProfileChatData>(getResult.Error);
            }
            var chatData = getResult.Result ?? new ProfileChatData();
            return new ExecuteResult<ProfileChatData>
            {
                Result = chatData
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SaveProfileChatDataAsync(string profileID, ProfileChatData data)
        {
            var rawData = JsonPlugin.ToJsonCompress(data);
            var saveResult = await SaveProfileInternalDataAsync(profileID, ProfileDataKeys.ProfileChatData, rawData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionChatMessagesResult>> GetMessagesFromChatAsync(string chatID, int count)
        {
            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChatMessagesResult>(dataProviderResult.Error);
            }

            var dataProvider = dataProviderResult.Result;
            var fixedCount = Math.Clamp(count, 0, MaxMessagesPerRequest);

            var getMessageResult = await dataProvider.GetMessagesFromChatAsync(chatID, fixedCount);
            if (getMessageResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChatMessagesResult>(getMessageResult.Error);
            }
            var messages = getMessageResult.Result;
            messages.Sort((x, y) => DateTime.Compare(x.CreationDateUTC, y.CreationDateUTC));

            return new ExecuteResult<FunctionChatMessagesResult>
            {
                Result = new FunctionChatMessagesResult
                {
                    Messages = messages
                }
            };
        }

        public static async Task<ExecuteResult<FunctionSendChatMessageResult>> SendProfileMessageToChatAsync(FunctionSendChatMessageRequest request)
        {
            // check inputs
            if (!request.IsValidInput())
            {
                return ErrorHandler.InvalidInput<FunctionSendChatMessageResult>();
            }
            var senderProfileID = request.SenderProfileID;
            var receiverProfileID = request.ReceiverProfileID;
            var chatID = request.ChatID;
            var contentType = request.ContentType;
            var customData = request.CustomData;
            var chatVisibilty = request.Visibility;

            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(dataProviderResult.Error);
            }
            var dataProvider = dataProviderResult.Result;

            var chatDataResult = await GetProfileChatDataAsync(senderProfileID);
            if (chatDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(chatDataResult.Error);
            }
            var profileChatData = chatDataResult.Result;

            var chatConfigResult = await GetChatConfigDataAsync();
            if (chatConfigResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(chatConfigResult.Error);
            }
            var chatConfig = chatConfigResult.Result;

            // check ban
            var isBanned = profileChatData.IsBanned(chatID, ServerTimeUTC);
            if (isBanned)
            {
                var banDetail = profileChatData.GetBanDetail(chatID);
                return ErrorHandler.ChatBan<FunctionSendChatMessageResult>(banDetail.BannedUntil);
            }

            var contentRawBody = string.Empty;
            var autoBanRequest = false;

            if (contentType == MessageContent.MESSAGE)
            {
                contentRawBody = request.MessageBody;
                // check max length
                var maxLength = chatConfig.GetMaxCharCount();
                if (contentRawBody.Length > maxLength)
                {
                    contentRawBody = contentRawBody.Substring(0, maxLength) + "...";
                }
                // check profanity
                if (chatConfig.AutomaticModeration)
                {
                    var hasCensoredWord = ChatCensor.HasCensoredWord(contentRawBody);
                    if (hasCensoredWord)
                    {
                        contentRawBody = ChatCensor.CensorText(contentRawBody);

                        if (chatConfig.AutoBan)
                        {
                            autoBanRequest = true;
                        }
                    }
                }
            }
            else if (contentType == MessageContent.STICKER)
            {
                var stickerID = request.StickerID;
                var stickerExist = chatConfig.StickerExist(stickerID);
                if (!stickerExist)
                {
                    return ErrorHandler.StickerNotFound<FunctionSendChatMessageResult>();
                }
                var chatSticker = new ChatSticker
                {
                    ID = stickerID
                };
                contentRawBody = JsonPlugin.ToJson(chatSticker);
            }
            else if (contentType == MessageContent.ITEM)
            {
                var inventoryItemID = request.ItemInstanceID;
                var getItemResult = await InventoryModule.GetItemInstanceByInventoryIDAsync(senderProfileID, inventoryItemID);
                if (getItemResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(getItemResult.Error);
                }
                var itemInstance = getItemResult.Result;
                contentRawBody = JsonPlugin.ToJson(itemInstance);
            }

            var senderMemberResult = await GetChatMemberAsync(senderProfileID);
            if (senderMemberResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(senderMemberResult.Error);
            }
            var senderMember = senderMemberResult.Result;
            
            ChatMember receiverMember = null;
            if (!string.IsNullOrEmpty(receiverProfileID))
            {
                var receiverMemberResult = await GetChatMemberAsync(receiverProfileID);
                if (receiverMemberResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(receiverMemberResult.Error);
                }
                receiverMember = receiverMemberResult.Result;
            }

            var messageID = dataProvider.GenerateNextMessageID();
            var message = new ChatMessage
            {
                MessageID = messageID,
                ChatID = chatID,
                ContentType = contentType,
                Target = ChatTarget.DEFAULT,
                Sender = senderMember,
                Visibility = chatVisibilty,
                TaggedProfile = receiverMember,
                CreationDateUTC = ServerTimeUTC,
                ContentRawData = contentRawBody,
                CustomData = customData
            };

            var sendMessageResult = await dataProvider.SendMessageAsync(chatID, message);
            if (sendMessageResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(sendMessageResult.Error);
            }

            if (chatVisibilty == ChatAccess.PRIVATE)
            {
                var interlocutorID = request.InterlocutorProfileID;
                var interlocutorMemberResult = await GetChatMemberAsync(interlocutorID);
                if (interlocutorMemberResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(interlocutorMemberResult.Error);
                }
                var interlocutorMember = interlocutorMemberResult.Result;

                var updateBadgeResult = await dataProvider.UpdateDialogListWithNewMessageAsync(senderMember, interlocutorMember, message);
                if (updateBadgeResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(updateBadgeResult.Error);
                }
            }

            // update chat activity
            var updateActivityResult = await dataProvider.UpdateChatActivityAsync(chatID, chatVisibilty, messageID);
            if (updateActivityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(updateActivityResult.Error);
            }

            // check auto ban
            if (autoBanRequest)
            {
                var autoBanHours = chatConfig.AutoBanSeconds;
                var autoBanReason = chatConfig.GetAutoBanReason();
                var banRequestResult = await AutoBanProfileInChatAsync(senderMember.ProfileID, chatID, autoBanHours, autoBanReason);
                if (banRequestResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(banRequestResult.Error);
                }
            }

            return new ExecuteResult<FunctionSendChatMessageResult>
            {
                Result = new FunctionSendChatMessageResult
                {
                    Message = message
                }
            };
        }

        public static async Task<ExecuteResult<FunctionSendChatMessageResult>> SendSystemMessageToChatAsync(string chatID, string messageText)
        {
            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(dataProviderResult.Error);
            }
            var dataProvider = dataProviderResult.Result;

            var messageID = dataProvider.GenerateNextMessageID();
            var message = new ChatMessage
            {
                MessageID = messageID,
                ChatID = chatID,
                ContentType = MessageContent.MESSAGE,
                Target = ChatTarget.SYSTEM,
                Visibility = ChatAccess.GROUP,
                CreationDateUTC = ServerTimeUTC,
                ContentRawData = messageText
            };

            var sendMessageResult = await dataProvider.SendMessageAsync(chatID, message);
            if (sendMessageResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(sendMessageResult.Error);
            }

            // update chat activity
            var updateActivityResult = await dataProvider.UpdateChatActivityAsync(chatID, ChatAccess.GROUP, messageID);
            if (updateActivityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSendChatMessageResult>(updateActivityResult.Error);
            }

            return new ExecuteResult<FunctionSendChatMessageResult>
            {
                Result = new FunctionSendChatMessageResult
                {
                    Message = message
                }
            };
        }

        public static async Task<ExecuteResult<FunctionModifyChatMessageResult>> ModifyMessageTextAsync(string profileID, string messageID, string chatID, string textToEdit)
        {
            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyChatMessageResult>(dataProviderResult.Error);
            }
            var dataProvider = dataProviderResult.Result;

            var chatDataResult = await GetProfileChatDataAsync(profileID);
            if (chatDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyChatMessageResult>(chatDataResult.Error);
            }
            var profileChatData = chatDataResult.Result;

            var getMessageResult = await dataProvider.GetMessageByIDAsync(messageID, chatID);
            if (getMessageResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyChatMessageResult>(getMessageResult.Error);
            }
            var message = getMessageResult.Result;

            var target = message.Target;
            if (target != ChatTarget.DEFAULT)
            {
                return ErrorHandler.ActionBlocked<FunctionModifyChatMessageResult>();
            }

            var sender = message.Sender;
            if (sender.ProfileID != profileID)
            {
                return ErrorHandler.ActionBlocked<FunctionModifyChatMessageResult>();
            }

            var contentType = message.ContentType;
            if (contentType != MessageContent.MESSAGE)
            {
                return ErrorHandler.ActionBlocked<FunctionModifyChatMessageResult>();
            }

            var state = message.State;
            if (state == MessageState.DELETED)
            {
                return ErrorHandler.ActionBlocked<FunctionModifyChatMessageResult>();
            }

            if (string.IsNullOrEmpty(textToEdit))
            {
                return ErrorHandler.InvalidInput<FunctionModifyChatMessageResult>();
            }

            var isBlocked = profileChatData.IsBanned(chatID, ServerTimeUTC);
            if (isBlocked)
            {
                return ErrorHandler.ActionBlocked<FunctionModifyChatMessageResult>();
            }

            message.ContentRawData = textToEdit;
            message.State = MessageState.MODIFIED;

            var saveResult = await dataProvider.ChangeMessageAsync(messageID, chatID, message);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyChatMessageResult>(saveResult.Error);
            }

            return new ExecuteResult<FunctionModifyChatMessageResult>
            {
                Result = new FunctionModifyChatMessageResult
                {
                    ModifiedMessage = message
                }
            };
        }

        public static async Task<ExecuteResult<FunctionModifyChatMessageResult>> DeleteMessageAsync(string profileID, string messageID, string chatID)
        {
            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyChatMessageResult>(dataProviderResult.Error);
            }
            var dataProvider = dataProviderResult.Result;

            var chatDataResult = await GetProfileChatDataAsync(profileID);
            if (chatDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyChatMessageResult>(chatDataResult.Error);
            }
            var profileChatData = chatDataResult.Result;

            var getMessageResult = await dataProvider.GetMessageByIDAsync(messageID, chatID);
            if (getMessageResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyChatMessageResult>(getMessageResult.Error);
            }
            var message = getMessageResult.Result;

            var target = message.Target;
            if (target != ChatTarget.DEFAULT)
            {
                return ErrorHandler.ActionBlocked<FunctionModifyChatMessageResult>();
            }

            var sender = message.Sender;
            var canDelete = sender.ProfileID == profileID || profileChatData.IsModerator;
            if (!canDelete)
            {
                return ErrorHandler.ActionBlocked<FunctionModifyChatMessageResult>();
            }

            var isBlocked = profileChatData.IsBanned(chatID, ServerTimeUTC);
            if (isBlocked)
            {
                return ErrorHandler.ActionBlocked<FunctionModifyChatMessageResult>();
            }
            message.State = MessageState.DELETED;

            var saveResult = await dataProvider.ChangeMessageAsync(messageID, chatID, message);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionModifyChatMessageResult>(saveResult.Error);
            }

            return new ExecuteResult<FunctionModifyChatMessageResult>
            {
                Result = new FunctionModifyChatMessageResult
                {
                    ModifiedMessage = message
                }
            };
        }

        public static async Task<ExecuteResult<ChatBanDetail>> BanProfileInChatAsync(string moderatorID, string bannedProfileID, string chatID, int banHours, string reason)
        {
            var chatDataResult = await GetProfileChatDataAsync(moderatorID);
            if (chatDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatBanDetail>(chatDataResult.Error);
            }
            var moderatorChatData = chatDataResult.Result;

            if (!moderatorChatData.IsModerator)
            {
                return ErrorHandler.ActionBlocked<ChatBanDetail>();
            }

            var profileChatDataResult = await GetProfileChatDataAsync(bannedProfileID);
            if (profileChatDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatBanDetail>(profileChatDataResult.Error);
            }
            var profileChatData = profileChatDataResult.Result;

            if (profileChatData.IsModerator)
            {
                return ErrorHandler.ActionBlocked<ChatBanDetail>();
            }

            var bannedUntil = ServerTimeUTC.AddHours(banHours);
            var banDetail = new ChatBanDetail
            {
                ChatID = chatID,
                BannedByModeratorID = moderatorID,
                Reason = reason,
                BannedUntil = bannedUntil
            };

            profileChatData.AddBan(chatID, banDetail);

            var saveResult = await SaveProfileChatDataAsync(bannedProfileID, profileChatData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatBanDetail>(saveResult.Error);
            }

            // generate ban message
            var chatConfigResult = await GetChatConfigDataAsync();
            if (chatConfigResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatBanDetail>(chatConfigResult.Error);
            }
            var chatConfig = chatConfigResult.Result;
            if (chatConfig.BanNotification)
            {
                var moderatorProfileResult = await TableProfileAssistant.GetProfileDetailAsync(moderatorID, new CBSProfileConstraints());
                if (moderatorProfileResult.Error != null)
                {
                    return ErrorHandler.ThrowError<ChatBanDetail>(moderatorProfileResult.Error);
                }
                var moderatorDetail = moderatorProfileResult.Result;

                var bannedProfileResult = await TableProfileAssistant.GetProfileDetailAsync(bannedProfileID, new CBSProfileConstraints());
                if (bannedProfileResult.Error != null)
                {
                    return ErrorHandler.ThrowError<ChatBanDetail>(bannedProfileResult.Error);
                }
                var bannedProfileDetail = bannedProfileResult.Result;

                var notificationTemplate = chatConfig.BanNotificationTemplate;
                var banMessage = string.Format(notificationTemplate, moderatorDetail.DisplayName, bannedProfileDetail.DisplayName, banHours, reason);

                var sendSystemMessageResult = await SendSystemMessageToChatAsync(chatID, banMessage);
                if (sendSystemMessageResult.Error != null)
                {
                    return ErrorHandler.ThrowError<ChatBanDetail>(sendSystemMessageResult.Error);
                }
            }

            return new ExecuteResult<ChatBanDetail>
            {
                Result = banDetail
            };
        }

        public static async Task<ExecuteResult<ChatBanDetail>> AutoBanProfileInChatAsync(string bannedProfileID, string chatID, int banSeconds, string reason)
        {
            var profileChatDataResult = await GetProfileChatDataAsync(bannedProfileID);
            if (profileChatDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatBanDetail>(profileChatDataResult.Error);
            }
            var profileChatData = profileChatDataResult.Result;

            if (profileChatData.IsModerator)
            {
                return ErrorHandler.ActionBlocked<ChatBanDetail>();
            }

            var bannedUntil = ServerTimeUTC.AddSeconds(banSeconds);
            var banDetail = new ChatBanDetail
            {
                ChatID = chatID,
                Reason = reason,
                BannedUntil = bannedUntil
            };

            profileChatData.AddBan(chatID, banDetail);

            var saveResult = await SaveProfileChatDataAsync(bannedProfileID, profileChatData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatBanDetail>(saveResult.Error);
            }

            // generate ban message
            var chatConfigResult = await GetChatConfigDataAsync();
            if (chatConfigResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatBanDetail>(chatConfigResult.Error);
            }
            var chatConfig = chatConfigResult.Result;
            if (chatConfig.BanNotification)
            {
                var bannedProfileResult = await TableProfileAssistant.GetProfileDetailAsync(bannedProfileID, new CBSProfileConstraints());
                if (bannedProfileResult.Error != null)
                {
                    return ErrorHandler.ThrowError<ChatBanDetail>(bannedProfileResult.Error);
                }
                var bannedProfileDetail = bannedProfileResult.Result;

                var notificationTemplate = ChatConfigData.SystemBanNotificationTemplate;
                var banMessage = string.Format(notificationTemplate, bannedProfileDetail.DisplayName, banSeconds, reason);

                var sendSystemMessageResult = await SendSystemMessageToChatAsync(chatID, banMessage);
                if (sendSystemMessageResult.Error != null)
                {
                    return ErrorHandler.ThrowError<ChatBanDetail>(sendSystemMessageResult.Error);
                }
            }

            return new ExecuteResult<ChatBanDetail>
            {
                Result = banDetail
            };
        }

        public static async Task<ExecuteResult<FunctionGetStickersPackResult>> GetStickersPackAsync()
        {
            var chatConfigResult = await GetChatConfigDataAsync();
            if (chatConfigResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetStickersPackResult>(chatConfigResult.Error);
            }
            var chatConfig = chatConfigResult.Result;
            var stickers = chatConfig.Stiсkers ?? new List<ChatSticker>();

            return new ExecuteResult<FunctionGetStickersPackResult>
            {
                Result = new FunctionGetStickersPackResult
                {
                    Stickers = stickers
                }
            };
        }

        public static async Task<ExecuteResult<TransferItemResult>> ClaimItemFromChatAsync(string profileID, string chatID, string messageID)
        {
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(messageID))
            {
                await using var handle = await locker.TryAcquireLockAsync(messageID);  
                var dataProviderResult = await GetChatDataProviderAsync();
                if (dataProviderResult.Error != null)
                {
                    return ErrorHandler.ThrowError<TransferItemResult>(dataProviderResult.Error);
                }
                var dataProvider = dataProviderResult.Result;

                var messageResult = await dataProvider.GetMessageByIDAsync(messageID, chatID);
                if (messageResult.Error != null)
                {
                    return ErrorHandler.ThrowError<TransferItemResult>(messageResult.Error);
                }
                var message = messageResult.Result;
                var contentType = message.ContentType;
                var owner = message.Sender;
                var state = message.State;

                if (contentType != MessageContent.ITEM || state == MessageState.DELETED || owner == null || owner.ProfileID == profileID)
                {
                    return ErrorHandler.ItemInstanceNotAvailable<TransferItemResult>();
                }

                var itemRawData = message.ContentRawData;
                var itemInstance = JsonPlugin.FromJson<ItemInstance>(itemRawData);
                var inventoryItemID = itemInstance.ItemInstanceId;

                var transferResult = await InventoryModule.TransferItemFromProfileToProfileAsync(inventoryItemID, owner.ProfileID, profileID);
                if (transferResult.Error != null)
                {
                    return ErrorHandler.ThrowError<TransferItemResult>(transferResult.Error);
                }

                return transferResult;
            }
        }

        public static async Task<ExecuteResult<ChatDialogEntry>> ClearDialogBadgeEntryAsync(string profileID, string withProfileID)
        {
            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatDialogEntry>(dataProviderResult.Error);
            }
            var dataProvider = dataProviderResult.Result;

            var clearResult = await dataProvider.ClearDialogBadgeWithProfileAsync(profileID, withProfileID);
            if (clearResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatDialogEntry>(clearResult.Error);
            }
            var dialogEntry = clearResult.Result;

            return new ExecuteResult<ChatDialogEntry>
            {
                Result = dialogEntry
            };
        }

        public static async Task<ExecuteResult<FunctionGetDialogListResult>> GetProfileDialogListAsync(string profileID)
        {
            var dataProviderResult = await GetChatDataProviderAsync();
            if (dataProviderResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetDialogListResult>(dataProviderResult.Error);
            }
            var dataProvider = dataProviderResult.Result;

            var getResult = await dataProvider.GetDialogListAsync(profileID, MaxDialogListCountPerProfile);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetDialogListResult>(getResult.Error);
            }

            var dialogList = getResult.Result.DialogList;

            return new ExecuteResult<FunctionGetDialogListResult>
            {
                Result = new FunctionGetDialogListResult
                {
                    DialogList = dialogList
                }
            };
        }

        public static async Task<ExecuteResult<FunctionBadgeResult>> GetDialogListBadgeAsync(string profileID)
        {
            var listResult = await GetProfileDialogListAsync(profileID);
            if (listResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBadgeResult>(listResult.Error);
            }
            var dialogList = listResult.Result.DialogList ?? new List<ChatDialogEntry>();
            var badgeCount = dialogList.Sum(x=>x.BadgeCount);

            return new ExecuteResult<FunctionBadgeResult>
            {
                Result = new FunctionBadgeResult
                {
                    Count = badgeCount
                }
            };
        }

        private static async Task<ExecuteResult<ChatConfigData>> GetChatConfigDataAsync()
        {
            if (!IsInited || ChatConfig == null)
            {
                var initResult = await InitChatModuleAsync();
                if (initResult.Error != null)
                {
                    return ErrorHandler.ThrowError<ChatConfigData>(initResult.Error);
                }
            }
            return new ExecuteResult<ChatConfigData>
            {
                Result = ChatConfig
            };
        }

        private static async Task<ExecuteResult<IChatDataProvider>> GetChatDataProviderAsync()
        {
            if (!IsInited || ChatDataProvider == null)
            {
                var initResult = await InitChatModuleAsync();
                if (initResult.Error != null)
                {
                    return ErrorHandler.ThrowError<ChatConfigData>(initResult.Error);
                }
            }
            return new ExecuteResult<IChatDataProvider>
            {
                Result = ChatDataProvider
            };
        }

        private static async Task<ExecuteResult<FunctionEmptyResult>> InitChatModuleAsync()
        {
            var getDataResult = await GetInternalTitleDataAsObjectAsync<ChatConfigData>(TitleKeys.ChatDataKey);
            if (getDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(getDataResult.Error);
            }
            ChatConfig = getDataResult.Result ?? new ChatConfigData();
            ChatDataProvider = new AzureChatDataProvider();
            var profanityList = ProfanityList.Profanity;
            var additionalProfanityList = ChatConfig.AdditionalBadWorldList ?? new List<string>();
            var fullProfanityList = profanityList.Concat(additionalProfanityList);
            ChatCensor = new Censor(fullProfanityList);
            IsInited = true;
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        private static async Task<ExecuteResult<ChatMember>> GetChatMemberAsync(string profileID)
        {
            var constrains = new CBSProfileConstraints
            {
                LoadAvatar = true
            };
            var getProfileResult = await TableProfileAssistant.GetProfileDetailAsync(profileID, constrains);
            if (getProfileResult.Error != null)
            {
                return ErrorHandler.ThrowError<ChatMember>(getProfileResult.Error);
            }
            var profile = getProfileResult.Result;
            var chatMember = new ChatMember
            {
                ProfileID = profile.ProfileID,
                DisplayName = profile.DisplayName,
                Avatar = profile.Avatar,
            };
            return new ExecuteResult<ChatMember>
            {
                Result = chatMember
            };
        }
    }
}