using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSChatModule : CBSModule, ICBSChat
    {
        /// <summary>
        /// Notify when the current user has cleared the conversation from unread messages.
        /// </summary>
        public event Action<ChatDialogEntry> OnUnreadMessageClear;

        /// <summary>
        /// Determines if the authorized profile is a moderator
        /// </summary>
        public bool IsModerator
        {
            get
            {
                if (ProfileChatData == null)
                    return false;
                return ProfileChatData.IsModerator;
            }
        }


        /// <summary>
        /// Information about profile for chats. Contain information about ban list
        /// </summary>
        public ProfileChatData ProfileChatData { get; private set; }

        private Dictionary<string, ChatInstance> ChatCache { get; set; } = new Dictionary<string, ChatInstance>();

        private string ServerID { get; set; }
        private string RegionID { get; set; }

        private IProfile Profile { get; set; }

        private IFabChat FabChat { get; set; }

        protected override void Init()
        {
            Profile = Get<CBSProfileModule>();
            FabChat = FabExecuter.Get<FabChat>();
        }

        /// <summary>
        /// Sets the server id for the server chat. You need to set this value before initializing the chat.
        /// </summary>
        /// <param name="serverID"></param>
        public void SetServerID(string serverID)
        {
            ServerID = serverID;
        }

        /// <summary>
        /// Sets the region ID for the regional chat. You need to set this value before initializing the chat. For example "ru", "en".
        /// </summary>
        /// <param name="region"></param>
        public void SetRegion(string region)
        {
            RegionID = region;
        }

        /// <summary>
        /// Get chat instance from pre-configured template. Global, Server, Regional.
        /// </summary>
        /// <param name="chatTitle"></param>
        /// <returns></returns>
        public ChatInstance GetOrCreateChat(ChatTitle chatTitle)
        {
            string chatID = GetChatID(chatTitle);
            bool exist = ChatCache.ContainsKey(chatID);
            var chatRequest = new ChatInstanceRequest
            {
                ChatID = chatID,
                LoadMessagesCount = CBSConstants.MaxChatHistory,
                Access = ChatAccess.GROUP
            };
            return exist ? ChatCache[chatID] : new ChatInstance(chatRequest);
        }

        /// <summary>
        /// Get chat instance from custom id. Suitable for creating group chats for example.
        /// </summary>
        /// <param name="chatID"></param>
        /// <returns></returns>
        public ChatInstance GetOrCreateGroupChatByID(string chatID)
        {
            var chatRequest = new ChatInstanceRequest
            {
                ChatID = chatID,
                LoadMessagesCount = CBSConstants.MaxChatHistory,
                Access = ChatAccess.GROUP
            };
            bool exist = ChatCache.ContainsKey(chatID);
            return exist ? ChatCache[chatID] : new ChatInstance(chatRequest);
        }

        /// <summary>
        /// Get private chat instance from custom id. Suitable for private chats, from lists of unread messages.
        /// </summary>
        /// <param name="chatID"></param>
        /// <returns></returns>
        public ChatInstance GetOrCreatePrivateChatWithProfile(string withProfileID)
        {
            string profileID = Profile.ProfileID;
            string[] userIds = new string[] { withProfileID, profileID };
            string chatID = GetChatIdBetweenTwoProfiles(withProfileID, profileID);

            var chatRequest = new ChatInstanceRequest
            {
                ChatID = chatID,
                LoadMessagesCount = CBSConstants.MaxChatHistory,
                Access = ChatAccess.PRIVATE,
                PrivateChatMembers = userIds
            };
            bool exist = ChatCache.ContainsKey(withProfileID);
            return exist ? ChatCache[withProfileID] : new ChatInstance(chatRequest);
        }

        /// <summary>
        /// Get list of main chat titles.
        /// </summary>
        /// <returns></returns>
        public List<ChatTitle> GetChatTitles()
        {
            return Enum.GetValues(typeof(ChatTitle)).Cast<ChatTitle>().ToList();
        }

        /// <summary>
        /// Get unique id for chat between two profiles
        /// </summary>
        /// <param name="profileID1"></param>
        /// <param name="profileID2"></param>
        /// <returns></returns>
        public string GetChatIdBetweenTwoProfiles(string profileID1, string profileID2)
        {
            var profilesArray = new string[] { profileID1, profileID2 };
            Array.Sort(profilesArray);
            return profilesArray[0] + profilesArray[1];
        }

        /// <summary>
        /// Get a list of the current profile conversations with whom there was previously a private conversation.
        /// </summary>
        /// <param name="result"></param>
        public void GetProfileDialogList(Action<CBSGetDialogListResult> result)
        {
            string profileID = Profile.ProfileID;
            FabChat.GetDialogList(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetDialogListResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetDialogListResult>();
                    var dialogList = functionResult.DialogList;
                    result?.Invoke(new CBSGetDialogListResult
                    {
                        IsSuccess = true,
                        DialogList = dialogList
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetDialogListResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get count of unread private messages
        /// </summary>
        /// <param name="result"></param>
        public void GetProfileDialogBadge(Action<CBSBadgeResult> result)
        {
            string profileID = Profile.ProfileID;
            FabChat.GetDialogBadge(profileID, onGet =>
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
        /// Clears the list of unread messages with a specific user.
        /// </summary>
        /// <param name="withProfileID"></param>
        /// <param name="result"></param>
        public void ClearDialogBadgeWithProfile(string withProfileID, Action<CBSClearDialogBadgeResult> result)
        {
            string profileID = Profile.ProfileID;
            FabChat.ClearDialogEntryBadge(profileID, withProfileID, onClear =>
            {
                var cbsError = onClear.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSClearDialogBadgeResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onClear.GetResult<ChatDialogEntry>();
                    result?.Invoke(new CBSClearDialogBadgeResult
                    {
                        IsSuccess = true,
                        UpdatedDialogEntry = functionResult
                    });
                    OnUnreadMessageClear?.Invoke(functionResult);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSClearDialogBadgeResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get messages from chat history. Max count = 1000
        /// </summary>
        /// <param name="chatID"></param>
        /// <param name="maxCount"></param>
        /// <param name="result"></param>
        public void GetMessagesFromChat(string chatID, int maxCount, Action<CBSGetMessagesFromChatResult> result)
        {
            FabChat.GetMessages(chatID, maxCount, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetMessagesFromChatResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionChatMessagesResult>();
                    var messages = functionResult.Messages;
                    result?.Invoke(new CBSGetMessagesFromChatResult
                    {
                        IsSuccess = true,
                        ChatID = chatID,
                        Messages = messages
                    });
                }
            },
            onFailed =>
            {
                result?.Invoke(new CBSGetMessagesFromChatResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Send text message to group chat
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        public void SendTextMessageToGroupChat(string chatID, CBSSendTextMessageRequest messageRequest, Action<CBSSendChatMessageResult> result)
        {
            var profileID = Profile.ProfileID;
            var message = new FunctionSendChatMessageRequest
            {
                ChatID = chatID,
                SenderProfileID = profileID,
                ContentType = MessageContent.MESSAGE,
                Target = ChatTarget.DEFAULT,
                Visibility = ChatAccess.GROUP,
                CustomData = messageRequest.CustomData,
                ReceiverProfileID = messageRequest.TaggedProfileID,
                MessageBody = messageRequest.MessageBody
            };
            InternalSendMessage(message, result);
        }

        /// <summary>
        /// Send sticker to group chat.
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        public void SendStickerMessageToGroupChat(string chatID, CBSSendStickerMessageRequest messageRequest, Action<CBSSendChatMessageResult> result)
        {
            var profileID = Profile.ProfileID;
            var message = new FunctionSendChatMessageRequest
            {
                ChatID = chatID,
                SenderProfileID = profileID,
                ContentType = MessageContent.STICKER,
                Target = ChatTarget.DEFAULT,
                Visibility = ChatAccess.GROUP,
                CustomData = messageRequest.CustomData,
                ReceiverProfileID = messageRequest.TaggedProfileID,
                StickerID = messageRequest.StickerID
            };
            InternalSendMessage(message, result);
        }

        /// <summary>
        /// Send item to group chat. Item can only be picked up by one chat member
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        public void SendItemMessageToGroupChat(string chatID, CBSSendItemMessageRequest messageRequest, Action<CBSSendChatMessageResult> result)
        {
            var profileID = Profile.ProfileID;
            var message = new FunctionSendChatMessageRequest
            {
                ChatID = chatID,
                SenderProfileID = profileID,
                ContentType = MessageContent.ITEM,
                Target = ChatTarget.DEFAULT,
                Visibility = ChatAccess.GROUP,
                CustomData = messageRequest.CustomData,
                ReceiverProfileID = messageRequest.TaggedProfileID,
                ItemInstanceID = messageRequest.InstanceID
            };
            InternalSendMessage(message, result);
        }

        /// <summary>
        /// Send text message to private chat between two profiles
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        public void SendTextMessageToPrivateChat(string withProfileID, CBSSendTextMessageRequest messageRequest, Action<CBSSendChatMessageResult> result)
        {
            var profileID = Profile.ProfileID;
            var chatID = GetChatIdBetweenTwoProfiles(profileID, withProfileID);
            var message = new FunctionSendChatMessageRequest
            {
                ChatID = chatID,
                SenderProfileID = profileID,
                ContentType = MessageContent.MESSAGE,
                Target = ChatTarget.DEFAULT,
                Visibility = ChatAccess.PRIVATE,
                CustomData = messageRequest.CustomData,
                ReceiverProfileID = messageRequest.TaggedProfileID,
                MessageBody = messageRequest.MessageBody,
                InterlocutorProfileID = withProfileID
            };
            InternalSendMessage(message, result);
        }

        /// <summary>
        /// Send sticker to private chat between two profiles
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        public void SendStickerMessageToPrivateChat(string withProfileID, CBSSendStickerMessageRequest messageRequest, Action<CBSSendChatMessageResult> result)
        {
            var profileID = Profile.ProfileID;
            var chatID = GetChatIdBetweenTwoProfiles(profileID, withProfileID);
            var message = new FunctionSendChatMessageRequest
            {
                ChatID = chatID,
                SenderProfileID = profileID,
                ContentType = MessageContent.STICKER,
                Target = ChatTarget.DEFAULT,
                Visibility = ChatAccess.PRIVATE,
                CustomData = messageRequest.CustomData,
                ReceiverProfileID = messageRequest.TaggedProfileID,
                StickerID = messageRequest.StickerID,
                InterlocutorProfileID = withProfileID
            };
            InternalSendMessage(message, result);
        }

        /// <summary>
        /// Send item to private chat between two profiles. Item can only be picked up by one chat member
        /// </summary>
        /// <param name="messageRequest"></param>
        /// <param name="result"></param>
        public void SendItemMessageToPrivateChat(string withProfileID, CBSSendItemMessageRequest messageRequest, Action<CBSSendChatMessageResult> result)
        {
            var profileID = Profile.ProfileID;
            var chatID = GetChatIdBetweenTwoProfiles(profileID, withProfileID);
            var message = new FunctionSendChatMessageRequest
            {
                ChatID = chatID,
                SenderProfileID = profileID,
                ContentType = MessageContent.ITEM,
                Target = ChatTarget.DEFAULT,
                Visibility = ChatAccess.PRIVATE,
                CustomData = messageRequest.CustomData,
                ReceiverProfileID = messageRequest.TaggedProfileID,
                ItemInstanceID = messageRequest.InstanceID,
                InterlocutorProfileID = withProfileID
            };
            InternalSendMessage(message, result);
        }

        /// <summary>
        /// Change message text property. Doesn't work with "Items" or "Stickers" messages
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="chatID"></param>
        /// <param name="textToChange"></param>
        /// <param name="result"></param>
        public void ChangeMessageText(string messageID, string chatID, string textToChange, Action<CBSModifyMessageResult> result)
        {
            var profileID = Profile.ProfileID;
            FabChat.ChangeMessageText(profileID, messageID, chatID, textToChange, onEdit =>
            {
                var cbsError = onEdit.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyMessageResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onEdit.GetResult<FunctionModifyChatMessageResult>();
                    var message = functionResult.ModifiedMessage;

                    result?.Invoke(new CBSModifyMessageResult
                    {
                        IsSuccess = true,
                        ModifiedMessage = message
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyMessageResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Mark chat message as deleted. Only the owner or moderator can delete a message
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="chatID"></param>
        /// <param name="result"></param>
        public void DeleteChatMessage(string messageID, string chatID, Action<CBSModifyMessageResult> result)
        {
            var profileID = Profile.ProfileID;
            FabChat.DeleteMessageText(profileID, messageID, chatID, onDelete =>
            {
                var cbsError = onDelete.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyMessageResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onDelete.GetResult<FunctionModifyChatMessageResult>();
                    var message = functionResult.ModifiedMessage;

                    result?.Invoke(new CBSModifyMessageResult
                    {
                        IsSuccess = true,
                        ModifiedMessage = message
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyMessageResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Mute profile in chat for a time. Action available only for moderators 
        /// </summary>
        /// <param name="profileIDForBan"></param>
        /// <param name="chatID"></param>
        /// <param name="reason"></param>
        /// <param name="banHours"></param>
        /// <param name="result"></param>
        public void BanProfile(string profileIDForBan, string chatID, string reason, int banHours, Action<CBSBanProfileInChatResult> result)
        {
            var profileID = Profile.ProfileID;
            FabChat.BanProfileInChat(profileID, profileIDForBan, chatID, banHours, reason, onBan =>
            {
                var cbsError = onBan.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBanProfileInChatResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onBan.GetResult<ChatBanDetail>();

                    result?.Invoke(new CBSBanProfileInChatResult
                    {
                        IsSuccess = true,
                        BanDetail = functionResult
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBanProfileInChatResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get list of available stickers
        /// </summary>
        /// <param name="result"></param>
        public void GetStickersPack(Action<CBSGetStickersResult> result)
        {
            FabChat.GetStickersPack(onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetStickersResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetStickersPackResult>();
                    var stickers = functionResult.Stickers;

                    result?.Invoke(new CBSGetStickersResult
                    {
                        IsSuccess = true,
                        Stickers = stickers
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetStickersResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Claim item from message if available.
        /// </summary>
        /// <param name="chatID"></param>
        /// <param name="messageID"></param>
        /// <param name="result"></param>
        public void ClaimItemFromChat(string chatID, string messageID, Action<CBSClaimItemFromMessageResult> result)
        {
            var profileID = Profile.ProfileID;
            FabChat.ClaimItemFromMessage(profileID, messageID, chatID, onDelete =>
            {
                var cbsError = onDelete.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSClaimItemFromMessageResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onDelete.GetResult<TransferItemResult>();
                    var itemInstance = functionResult.Item;
                    var ownerID = functionResult.OwnerID;
                    var cbsItem = itemInstance.ToCBSInventoryItem();

                    result?.Invoke(new CBSClaimItemFromMessageResult
                    {
                        IsSuccess = true,
                        SenderProfileID = ownerID,
                        GrantedItem = cbsItem
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSClaimItemFromMessageResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        // internal
        private void InternalSendMessage(FunctionSendChatMessageRequest request, Action<CBSSendChatMessageResult> result)
        {
            FabChat.SendMessage(request, onSent =>
            {
                var cbsError = onSent.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSSendChatMessageResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onSent.GetResult<FunctionSendChatMessageResult>();
                    var message = functionResult.Message;
                    result?.Invoke(new CBSSendChatMessageResult
                    {
                        IsSuccess = true,
                        Message = message
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSSendChatMessageResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        private string GetChatID(ChatTitle chatTitle)
        {
            switch (chatTitle)
            {
                case ChatTitle.GLOBAL:
                    return CBSConstants.ChatGlobalID;
                case ChatTitle.REGIONAL:
                    return string.IsNullOrEmpty(RegionID) ? CBSConstants.ChatDefaultRegion : RegionID;
                case ChatTitle.SERVER:
                    return string.IsNullOrEmpty(ServerID) ? CBSConstants.ChatDefaultServer : ServerID;
            }
            return string.Empty;
        }

        internal void ParseChatData(ProfileChatData chatData)
        {
            ProfileChatData = chatData;
        }

        protected override void OnLogout()
        {
            foreach (var keyPair in ChatCache)
            {
                keyPair.Value.Dispose();
            }
            ChatCache = new Dictionary<string, ChatInstance>();
        }
    }
}
