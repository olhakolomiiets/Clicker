using System;
using System.Collections.Generic;

namespace CBS.Models
{
    public class ProfileChatData
    {
        public bool IsModerator;
        public Dictionary<string, ChatBanDetail> BanDictionary;

        public bool IsBanned(string chatID, DateTime serverTime)
        {
            if (BanDictionary == null)
                return false;
            if (!BanDictionary.ContainsKey(chatID))
                return false;
            var banDetail = BanDictionary[chatID];
            var bannedUntil = banDetail.BannedUntil;
            return serverTime.Ticks < bannedUntil.Ticks;
        }

        public ChatBanDetail GetBanDetail(string chatID)
        {
            if (BanDictionary == null)
                return null;
            if (!BanDictionary.ContainsKey(chatID))
                return null;
            var banDetail = BanDictionary[chatID];
            return banDetail;
        }

        public void AddBan(string chatID, ChatBanDetail banDetail)
        {
            BanDictionary = BanDictionary ?? new Dictionary<string, ChatBanDetail>();
            BanDictionary[chatID] = banDetail;
        }
    }
}
