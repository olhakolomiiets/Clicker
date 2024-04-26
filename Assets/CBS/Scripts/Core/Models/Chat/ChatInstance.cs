using CBS.Context;
using CBS.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CBS
{
    public class ChatInstance : IChat
    {
        public event Action<ChatMessage> OnNewMessage;
        public event Action<List<ChatMessage>> OnGetHistory;

        public string ChatID { get; private set; }
        public string[] PrivateChatMembers { get; private set; }

        private int HistoryMaxCount { get; set; }
        private int NewMessageCompareCount { get; set; }
        private float CompareWait { get; set; }
        private List<ChatMessage> CacheMessages { get; set; }
        private List<ChatMessage> CompareList { get; set; }
        private ICBSChat ChatModule { get; set; }
        private IProfile Profile { get; set; }
        private ChatAccess ChatType { get; set; }
        private ICoroutineRunner CoroutineRunner { get; set; }
        private Coroutine CheckNewMessageCoroutine { get; set; }

        public ChatInstance(ChatInstanceRequest request)
        {
            HistoryMaxCount = request.LoadMessagesCount;
            ChatModule = CBSModule.Get<CBSChatModule>();
            Profile = CBSModule.Get<CBSProfileModule>();
            CoroutineRunner = ChatModule.CoroutineRunner;
            ChatID = request.ChatID;
            ChatType = request.Access;
            PrivateChatMembers = request.PrivateChatMembers;
            NewMessageCompareCount = CBSConstants.ChatCompareCount;
            CompareWait = CBSConstants.ChatCompareWait;
        }

        public void Load()
        {
            GetChatHistory();
            StartCheckNewMessages();
        }

        private string GetInterlocutorID()
        {
            if (ChatType != ChatAccess.PRIVATE)
                return string.Empty;
            var profileID = Profile.ProfileID;
            var interlocutorID = PrivateChatMembers.Where(x => x != profileID).FirstOrDefault();
            return interlocutorID;
        }

        public void SetMaxMessagesCount(int maxMessagesToLoad)
        {
            HistoryMaxCount = maxMessagesToLoad;
        }

        public void SetUpdateIntervalMiliseconds(float updateInterval)
        {
            CompareWait = updateInterval;
        }

        public void SendMessage(CBSSendTextMessageRequest request, Action<CBSSendChatMessageResult> result = null)
        {
            if (ChatType == ChatAccess.GROUP)
            {
                ChatModule.SendTextMessageToGroupChat(ChatID, request, result);
            }
            else if (ChatType == ChatAccess.PRIVATE)
            {
                ChatModule.SendTextMessageToPrivateChat(GetInterlocutorID(), request, result);
            }
        }

        public void SendSticker(CBSSendStickerMessageRequest request, Action<CBSSendChatMessageResult> result = null)
        {
            if (ChatType == ChatAccess.GROUP)
            {
                ChatModule.SendStickerMessageToGroupChat(ChatID, request, result);
            }
            else if (ChatType == ChatAccess.PRIVATE)
            {
                ChatModule.SendStickerMessageToPrivateChat(GetInterlocutorID(), request, result);
            }
        }

        public void SendItem(CBSSendItemMessageRequest request, Action<CBSSendChatMessageResult> result = null)
        {
            if (ChatType == ChatAccess.GROUP)
            {
                ChatModule.SendItemMessageToGroupChat(ChatID, request, result);
            }
            else if (ChatType == ChatAccess.PRIVATE)
            {
                ChatModule.SendItemMessageToPrivateChat(GetInterlocutorID(), request, result);
            }
        }

        public void GetChatHistory(Action<CBSGetMessagesFromChatResult> result = null)
        {
            ChatModule.GetMessagesFromChat(ChatID, HistoryMaxCount, onGet =>
            {
                if (onGet.IsSuccess)
                {
                    CacheMessages = onGet.Messages;
                    OnGetHistory?.Invoke(CacheMessages);
                }
                result?.Invoke(onGet);
            });
        }

        private void StartCheckNewMessages()
        {
            CheckNewMessageCoroutine = CoroutineRunner.StartCoroutine(CheckNewMessagesCoroutine());
        }

        private void StopCheckNewMessages()
        {
            try
            {
                if (CheckNewMessageCoroutine != null && CoroutineRunner != null)
                {
                    CoroutineRunner?.StopCoroutine(CheckNewMessageCoroutine);
                }
                CheckNewMessageCoroutine = null;
            }
            catch { }
        }

        private IEnumerator CheckNewMessagesCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(CompareWait / 1000f);
                yield return new WaitUntil(() => CacheMessages != null);
                CBSGetMessagesFromChatResult messagesResult = null;
                ChatModule.GetMessagesFromChat(ChatID, NewMessageCompareCount, onGet =>
                {
                    messagesResult = onGet;
                });
                yield return new WaitUntil(() => messagesResult != null);
                if (messagesResult != null && messagesResult.IsSuccess)
                {
                    CompareList = messagesResult.Messages;
                    foreach (var messageToCheck in CompareList)
                    {
                        var contain = CacheMessages.Any(x => x.MessageID == messageToCheck.MessageID);
                        if (!contain)
                        {
                            CacheMessages.Add(messageToCheck);
                            OnNewMessage?.Invoke(messageToCheck);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            CacheMessages = null;
            StopCheckNewMessages();
        }
    }
}
