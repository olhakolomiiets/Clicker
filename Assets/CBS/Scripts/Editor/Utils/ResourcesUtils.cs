using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public static class ResourcesUtils
    {
        private static readonly string TexturePath = "Assets/CBS/Content/Editor/";
        private static readonly string AvatarsPath = "Assets/CBS/Content/Sprites/Avatars/";
        private static readonly string SimpleClanIconsPath = "Assets/CBS/Content/Sprites/Clan/";
        private static readonly string ComplexClanBackgroundsPath = "Assets/CBS/Content/Sprites/Clan/Backgrounds/";
        private static readonly string ComplexClanForegroundsPath = "Assets/CBS/Content/Sprites/Clan/Foregrounds/";
        private static readonly string ComplexClanEmblemsPath = "Assets/CBS/Content/Sprites/Clan/Emblems/";
        private static readonly string StickersPath = "Assets/CBS/Content/Sprites/Stickers/";

        public static Texture GetMenuTexture(MenuTitles title, ButtonState state)
        {
            var imagePath = string.Empty;
            switch (title)
            {
                case MenuTitles.Auth:
                    imagePath = state == ButtonState.Default ? "auth_default.png" : "auth_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Profile:
                    imagePath = state == ButtonState.Default ? "profile_default.png" : "profile_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Currency:
                    imagePath = state == ButtonState.Default ? "currency_default.png" : "currency_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Items:
                    imagePath = state == ButtonState.Default ? "items_default.png" : "items_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Azure:
                    imagePath = state == ButtonState.Default ? "azure_default.png" : "azure_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Chat:
                    imagePath = state == ButtonState.Default ? "chat_default.png" : "chat_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Clans:
                    imagePath = state == ButtonState.Default ? "clan_default.png" : "clan_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Tournaments:
                    imagePath = state == ButtonState.Default ? "tournament_default.png" : "tournament_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Calendar:
                    imagePath = state == ButtonState.Default ? "calendar_default.png" : "calendar_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Roulette:
                    imagePath = state == ButtonState.Default ? "roulette_default.png" : "roulette_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.PlayFab:
                    imagePath = state == ButtonState.Default ? "playfab_default.png" : "playfab_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Matchmaking:
                    imagePath = state == ButtonState.Default ? "matchmaking_default.png" : "matchmaking_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Achievements:
                    imagePath = state == ButtonState.Default ? "achievemets_default.png" : "achievements_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.DailyTasks:
                    imagePath = state == ButtonState.Default ? "tasks_default.png" : "tasks_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Leaderboards:
                    imagePath = state == ButtonState.Default ? "leaderboards_default.png" : "leaderboards_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.BattlePass:
                    imagePath = state == ButtonState.Default ? "battle_pass_default.png" : "battle_pass_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.IAP:
                    imagePath = state == ButtonState.Default ? "iap_default.png" : "iap_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Friends:
                    imagePath = state == ButtonState.Default ? "friends_default.png" : "friends_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.TitleData:
                    imagePath = state == ButtonState.Default ? "title_default.png" : "title_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Events:
                    imagePath = state == ButtonState.Default ? "events_default.png" : "events_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Store:
                    imagePath = state == ButtonState.Default ? "store_default.png" : "store_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Story:
                    imagePath = state == ButtonState.Default ? "story_default.png" : "story_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Notification:
                    imagePath = state == ButtonState.Default ? "notification_default.png" : "notification_active.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                default:
                    return null;
            }
        }

        public static Texture GetTitleTexture(MenuTitles title)
        {
            var imagePath = string.Empty;
            switch (title)
            {
                case MenuTitles.Auth:
                    imagePath = "Titles/auth_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Profile:
                    imagePath = "Titles/profile_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Currency:
                    imagePath = "Titles/currency_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Items:
                    imagePath = "Titles/items_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Azure:
                    imagePath = "Titles/azure_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Chat:
                    imagePath = "Titles/chat_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Clans:
                    imagePath = "Titles/clan_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Tournaments:
                    imagePath = "Titles/tournament_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Calendar:
                    imagePath = "Titles/calendar_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Roulette:
                    imagePath = "Titles/roulette_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.PlayFab:
                    imagePath = "Titles/playfab_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Matchmaking:
                    imagePath = "Titles/matchmaking_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Achievements:
                    imagePath = "Titles/achievements_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.DailyTasks:
                    imagePath = "Titles/tasks_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Leaderboards:
                    imagePath = "Titles/leaderboards_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.BattlePass:
                    imagePath = "Titles/battle_pass_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.IAP:
                    imagePath = "Titles/iap_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Friends:
                    imagePath = "Titles/friends_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.TitleData:
                    imagePath = "Titles/title_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Events:
                    imagePath = "Titles/events_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Store:
                    imagePath = "Titles/store_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Story:
                    imagePath = "Titles/story_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                case MenuTitles.Notification:
                    imagePath = "Titles/notification_title.png";
                    return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + imagePath, typeof(Texture));
                default:
                    return null;
            }
        }

        public static Sprite GetAvatar(string avatarID)
        {
            var avatarPath = avatarID + ".png";
            return (Sprite)AssetDatabase.LoadAssetAtPath(AvatarsPath + avatarPath, typeof(Sprite));
        }

        public static Sprite GetSticker(string stickerID)
        {
            var avatarPath = stickerID + ".png";
            return (Sprite)AssetDatabase.LoadAssetAtPath(StickersPath + avatarPath, typeof(Sprite));
        }

        public static Sprite GetSimpleClanIcon(string iconID)
        {
            var iconPath = iconID + ".png";
            return (Sprite)AssetDatabase.LoadAssetAtPath(SimpleClanIconsPath + iconPath, typeof(Sprite));
        }

        public static Sprite GetBackgroundClanIcon(string iconID)
        {
            var iconPath = iconID + ".png";
            return (Sprite)AssetDatabase.LoadAssetAtPath(ComplexClanBackgroundsPath + iconPath, typeof(Sprite));
        }

        public static Sprite GetForegroundClanIcon(string iconID)
        {
            var iconPath = iconID + ".png";
            return (Sprite)AssetDatabase.LoadAssetAtPath(ComplexClanForegroundsPath + iconPath, typeof(Sprite));
        }

        public static Sprite GetEmblemsClanIcon(string iconID)
        {
            var iconPath = iconID + ".png";
            return (Sprite)AssetDatabase.LoadAssetAtPath(ComplexClanEmblemsPath + iconPath, typeof(Sprite));
        }

        public static Texture GetBackgroundImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "background2.png", typeof(Texture));
        }

        public static Texture GetMatchImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "Matchmaking/MatchIcon.png", typeof(Texture));
        }

        public static Texture GetLeaderboardImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "Leaderboards/LeaderboardIcon.png", typeof(Texture));
        }

        public static Texture GetProfileImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "Profile/profileIcon.png", typeof(Texture));
        }

        public static Texture GetRewardImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "General/reward.png", typeof(Texture));
        }

        public static Texture GetRedDotImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "General/redDot.png", typeof(Texture));
        }

        public static Texture GetGreyDotImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "General/greyDot.png", typeof(Texture));
        }

        public static Texture GetTextureByPath(string path)
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + path, typeof(Texture));
        }

        public static Texture GetRealMoneyImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "Currency/money.png", typeof(Texture));
        }

        public static Texture GetBankImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "BattlePass/Bank.png", typeof(Texture));
        }

        public static Texture GetMessageImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "Notification/Message.png", typeof(Texture));
        }

        public static Texture GetLightningImage()
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(TexturePath + "Events/lightning.png", typeof(Texture));
        }

        public static Texture2D GetEventNormalImage()
        {
            return (Texture2D)AssetDatabase.LoadAssetAtPath(TexturePath + "Events/event_default.png", typeof(Texture2D));
        }

        public static Texture2D GetEventActiveImage()
        {
            return (Texture2D)AssetDatabase.LoadAssetAtPath(TexturePath + "Events/event_running.png", typeof(Texture2D));
        }

        public static Texture2D GetEventWaitingImage()
        {
            return (Texture2D)AssetDatabase.LoadAssetAtPath(TexturePath + "Events/event_waiting.png", typeof(Texture2D));
        }
    }

    public enum ButtonState
    {
        Default,
        Active
    }

}
