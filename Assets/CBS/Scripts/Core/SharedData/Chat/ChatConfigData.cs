

using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class ChatConfigData
    {
        public const int MAX_HISTORY_LENGTH = 1000;

        public static readonly int MaxMessageLength = 3000;
        public static readonly int DefaultMessageLength = 256;
        public static readonly int DefaultMessageTTL = 864000;
        public static readonly int DefaultAutoBanSeconds = 1800;
        public static readonly int DefaultDontRemoveCount = 100;
        public static string DefaultCensorChar = "*";
        public static string DefaultBanNotificationTemplate = "Moderator {0} banned user {1} for {2} hours due to - {3}";
        public static readonly string DefaultAutoBanReason = "Obscene language in the chat";
        public static string SystemBanNotificationTemplate = "Profile {0} was banned for {1} seconds from chat for {2}";

        public int MaxCharCountPerMessage;
        public List<ChatSticker> Stiсkers;
        public CBSTTL GeneralChatTTL;
        public int? GeneralChatSecondsTTL;
        public int? GeneralChatDontRemoveCount;
        public CBSTTL PrivateChatTTL;
        public int? PrivateChatSecondsTTL;
        public int? PrivateChatDontRemoveCount;
        public bool AutomaticModeration;
        public List<string> AdditionalBadWorldList;
        public string CensorChar;
        public bool AutoBan;
        public int AutoBanSeconds;
        public string AutoBanReason;
        public bool BanNotification;
        public string BanNotificationTemplate;

        public int GetGeneralTTL()
        {
            if (GeneralChatTTL == CBSTTL.NEVER_EXPIRED)
            {
                return -1;
            }
            else
            {
                if (GeneralChatSecondsTTL == null)
                    return -1;
                return (int)GeneralChatSecondsTTL;
            }
        }

        public int GetPrivateTTL()
        {
            if (PrivateChatTTL == CBSTTL.NEVER_EXPIRED)
            {
                return -1;
            }
            else
            {
                if (PrivateChatSecondsTTL == null)
                    return -1;
                return (int)PrivateChatSecondsTTL;
            }
        }

        public int GetDontRemoveCount(ChatAccess access)
        {
            if (access == ChatAccess.GROUP)
            {
                if (GeneralChatDontRemoveCount == null)
                {
                    return DefaultDontRemoveCount;
                }
                return (int)GeneralChatDontRemoveCount;
            }
            else if (access == ChatAccess.PRIVATE)
            {
                if (PrivateChatDontRemoveCount == null)
                {
                    return DefaultDontRemoveCount;
                }
                return (int)PrivateChatDontRemoveCount;
            }
            return DefaultDontRemoveCount;
        }

        public int GetMaxCharCount()
        {
            if (MaxCharCountPerMessage <= 0)
            {
                return DefaultMessageLength;
            }
            else if (MaxCharCountPerMessage > MaxMessageLength)
            {
                return MaxMessageLength;
            }
            return MaxCharCountPerMessage;
        }

        public List<string> GetBadWordList()
        {
            AdditionalBadWorldList = AdditionalBadWorldList ?? new List<string>();
            return AdditionalBadWorldList;
        }

        public void AddBadWord(string word)
        {
            AdditionalBadWorldList = AdditionalBadWorldList ?? new List<string>();
            if (!AdditionalBadWorldList.Contains(word))
            {
                AdditionalBadWorldList.Add(word);
            }
        }

        public void RemoveBadWord(string word)
        {
            if (AdditionalBadWorldList == null)
                return;
            if (AdditionalBadWorldList.Contains(word))
            {
                AdditionalBadWorldList.Remove(word);
            }
        }

        public string GetCensorChar()
        {
            if (string.IsNullOrEmpty(CensorChar))
                return DefaultCensorChar;
            return CensorChar;
        }

        public int GetAutoBanDuration()
        {
            if (AutoBanSeconds < 1)
            {
                return DefaultAutoBanSeconds;
            }
            return AutoBanSeconds;
        }

        public void AddSticker(ChatSticker sticker)
        {
            Stiсkers = Stiсkers ?? new List<ChatSticker>();
            Stiсkers.Add(sticker);
        }

        public void RemoveSticker(ChatSticker sticker)
        {
            if (Stiсkers == null)
                return;
            Stiсkers.Remove(sticker);
        }

        public bool StickerExist(string stickerID)
        {
            if (Stiсkers == null)
                return false;
            return Stiсkers.Any(x => x.ID == stickerID);
        }

        public string GetBanNotificationTemplate()
        {
            if (string.IsNullOrEmpty(BanNotificationTemplate))
            {
                return DefaultBanNotificationTemplate;
            }
            return BanNotificationTemplate;
        }

        public string GetAutoBanReason()
        {
            if (string.IsNullOrEmpty(AutoBanReason))
            {
                return DefaultAutoBanReason;
            }
            return AutoBanReason;
        }
    }
}
