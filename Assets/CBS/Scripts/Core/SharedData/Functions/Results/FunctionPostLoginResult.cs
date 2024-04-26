using System.Collections.Generic;
using CBS.SharedData.Lootbox;

namespace CBS.Models
{
    public class FunctionPostLoginResult
    {
        public string ProfileID;
        public string DisplayName;
        public string AvatarID;
        public string ClanID;
        public string ClanRoleID;
        public LevelInfo PlayerLevelInfo;
        public PlayFab.ClientModels.GetCatalogItemsResult ItemsResult;
        public Dictionary<string, string> CategoriesResult;
        public List<string> CurrencyPacksIDs;
        public List<string> CalendarCatalogIDs;
        public List<string> TicketsCatalogIDs;
        public ProfileChatData ProfileChatData;
        public ClanEntity ClanEntity;
        public bool OverridedNewPlayerValue;

        public CBSRecipeContainer Recipes;
        public CBSItemUpgradesContainer Upgrades;
        public CBSLootboxTable LootboxTable;
    }
}
