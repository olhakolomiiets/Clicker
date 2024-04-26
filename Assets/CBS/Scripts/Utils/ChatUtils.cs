using CBS.Models;
using CBS.Scriptable;
using CBS.UI;
using PlayFab.ClientModels;
using System;
using System.Text;
using UnityEngine;

namespace CBS.Utils
{
    public static class ChatUtils
    {
        public const int DefaultBanHours = 3;
        public const string DefaultBanReason = "Chat rule violation";
        public const string SystemMessageTitle = "System";
        public const string DeletedMessageText = "This message has been deleted";
        private static readonly string DateFormat = "HH:mm:ss";
        public const string ClaimItemTitle = "Success";

        public static string MarkNickName(string inputName)
        {
            return "[<b>" + inputName + "</b>]";
        }

        public static string MarkAsItalic(string inputText)
        {
            return "<i>" + inputText + "</i>";
        }

        public static string MarkDate(DateTime date)
        {
            return "[<b>" + date.ToLocalTime().ToString(DateFormat) + "</b>]";
        }

        public static StringBuilder MarkNickName(StringBuilder sBuilder, string inputName)
        {
            sBuilder.Append("[<b>");
            sBuilder.Append(inputName);
            sBuilder.Append("</b>]");
            return sBuilder;
        }

        public static string AddColorTag(Color color, string inputText)
        {
            var htmlColor = ColorUtility.ToHtmlStringRGB(color);
            return string.Format("<color=#{0}>{1}</color>", htmlColor, inputText);
        }

        public static StringBuilder AddColorTag(StringBuilder sBuilder, Color color)
        {
            var htmlColor = ColorUtility.ToHtmlStringRGB(color);
            sBuilder.Insert(0, string.Format("<color=#{0}>", htmlColor));
            sBuilder.Append("</color>");
            return sBuilder;
        }

        public static string MarkAsMine(string inputText)
        {
            return "<color=orange>" + inputText + "</color>";
        }

        public static Color GetMineBubbleColor()
        {
            return new Color(159f / 255f, 255f / 255f, 150f / 255f);
        }

        public static void ShowSimpleChat(ChatInstance chat)
        {
            var prefabs = CBSScriptable.Get<ChatPrefabs>();
            var chatPrefab = prefabs.SimpleChatView;
            var activeChatInstance = UIView.GetInstance(chatPrefab);
            if (activeChatInstance != null)
            {
                UIView.HideWindow(activeChatInstance);
            }
            activeChatInstance = UIView.ShowWindow(chatPrefab);
            activeChatInstance.GetComponent<ChatView>().Init(chat);
        }

        public static ChatSticker GetSticker(this ChatMessage message)
        {
            if (string.IsNullOrEmpty(message.ContentRawData))
                return null;
            try
            {
                return JsonPlugin.FromJson<ChatSticker>(message.ContentRawData);
            }
            catch { }
            return null;
        }

        public static CBSInventoryItem GetItem(this ChatMessage message)
        {
            if (string.IsNullOrEmpty(message.ContentRawData))
                return null;
            try
            {
                var itemInstance = JsonPlugin.FromJson<ItemInstance>(message.ContentRawData);
                var cbsItem = itemInstance.ToCBSInventoryItem();
                return cbsItem;
            }
            catch { }
            return null;
        }
    }
}
