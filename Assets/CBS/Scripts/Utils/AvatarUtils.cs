using CBS.Models;
using CBS.Scriptable;
using UnityEngine;

namespace CBS
{
    public static class AvatarUtils
    {
        public static Sprite GetSprite(this CBSSpriteAvatar avatar)
        {
            var spriteData = CBSScriptable.Get<AvatarIcons>();
            return spriteData == null ? null : spriteData.GetSprite(avatar.ID);
        }

        public static Sprite GetSprite(this CBSItem item)
        {
            var spriteData = CBSScriptable.Get<ItemsIcons>();
            return spriteData == null ? null : spriteData.GetSprite(item.ItemID);
        }

        public static Sprite GetSprite(this CBSBaseItem item)
        {
            var spriteData = CBSScriptable.Get<ItemsIcons>();
            return spriteData == null ? null : spriteData.GetSprite(item.ItemID);
        }

        public static Sprite GetSprite(this CBSInventoryItem item)
        {
            var spriteData = CBSScriptable.Get<ItemsIcons>();
            return spriteData == null ? null : spriteData.GetSprite(item.ItemID);
        }

        public static Sprite GetSprite(this CBSCurrency item)
        {
            var spriteData = CBSScriptable.Get<CurrencyIcons>();
            return spriteData == null ? null : spriteData.GetSprite(item.Code);
        }

        public static Sprite GetSprite(this CBSCurrencyPack item)
        {
            var spriteData = CBSScriptable.Get<CurrencyIcons>();
            return spriteData == null ? null : spriteData.GetSprite(item.ID);
        }

        public static Sprite GetSprite(this CBSStoreItem item)
        {
            var spriteData = CBSScriptable.Get<StoreIcons>();
            return spriteData == null ? null : spriteData.GetSprite(item.ItemID);
        }

        public static Sprite GetSprite(this CalendarInstance item)
        {
            var spriteData = CBSScriptable.Get<CalendarIcons>();
            return spriteData == null ? null : spriteData.GetSprite(item.ID);
        }

        public static Sprite GetSprite(this RoulettePosition item)
        {
            var spriteData = CBSScriptable.Get<ItemsIcons>();
            return spriteData == null ? null : spriteData.GetSprite(item.ID);
        }

        public static Sprite GetSprite(this CBSTask item)
        {
            var spriteData = CBSScriptable.Get<TasksIcons>();
            var spriteID = item.PoolID + item.ID;
            return spriteData == null ? null : spriteData.GetSprite(spriteID);
        }

        public static Sprite GetSprite(this ChatSticker sticker)
        {
            var spriteData = CBSScriptable.Get<StickersIcons>();
            var spriteID = sticker.ID;
            return spriteData == null ? null : spriteData.GetSprite(spriteID);
        }

        public static Sprite GetBackgroundSprite(this CBSEvent eventInstance)
        {
            var spriteData = CBSScriptable.Get<EventsIcons>();
            var spriteID = eventInstance.ID;
            return spriteData == null ? null : spriteData.GetBackgroundSprite(spriteID);
        }

        public static Sprite GetIconSprite(this CBSEvent eventInstance)
        {
            var spriteData = CBSScriptable.Get<EventsIcons>();
            var spriteID = eventInstance.ID;
            return spriteData == null ? null : spriteData.GetSprite(spriteID);
        }
    }
}
