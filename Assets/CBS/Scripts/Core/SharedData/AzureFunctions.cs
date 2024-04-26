using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CBS
{
    public static class AzureFunctions
    {
        // profile
        public const string AddExperienceToPlayerMethod = "AddExperienceToPlayer";
        public const string UpdateProfileDisplayNameMethod = "UpdateProfileDisplayName";
        public const string GetProfileExperienceMethod = "GetProfileExperienceData";
        public const string GetProfileDetailMethod = "GetProfileDetail";
        public const string GetProfileDetailByDisplayNameMethod = "GetProfileDetailByDisplayName";
        public const string GetProfilesDetailsMethod = "GetProfilesDetails";
        public const string GetProfilesDataMethod = "GetProfilesData";
        public const string UpdateProfileOnlineStateMethod = "UpdateProfileOnlineState";
        public const string SetProfilesDataMethod = "SetProfilesData";
        public const string SetProfilesMultiplyDataMethod = "SetProfilesMultiplyData";
        public const string GetProfileLevelTableMethod = "GetProfileLevelTable";
        public const string UpdateProfileImageURLMethod = "UpdateProfileImageURL";
        public const string UpdateProfileAvatarIDMethod = "UpdateProfileAvatarID";
        public const string PurchaseProfileAvatarMethod = "PurchaseProfileAvatar";
        public const string GrantProfileAvatarMethod = "GrantProfileAvatar";
        public const string DeleteMasterPlayerAccountMethod = "DeleteMasterPlayerAccount";

        // currency
        public const string AddProfileCurrencyMethod = "AddProfileVirtualCurrency";
        public const string SubtractProfileCurrencyMethod = "SubtractProfileVirtualCurrency";
        public const string GetProfileCurrencyMethod = "GetProfileVirtualCurrency";
        public const string GetCurrenciesPackMethod = "GetCurrenciesPack";
        public const string GrantCurrencyPackMethod = "GrantCurrencyPack";

        public const string FindMatchMethod = "FindMatch";
        public const string CloseRoomVisibilityMethod = "CloseRoomVisibility";
        public const string GetTournamentStateMethod = "GetTournamentState";
        public const string FindAndJoinTournamentMethod = "FindAndJoinTournament";
        public const string LeaveTournamentMethod = "LeaveTournament";
        public const string UpdatePlayerTournamentPointMethod = "UpdatePlayerTournamentPoint";
        public const string AddPlayerTournamentPointMethod = "AddPlayerTournamentPoint";
        public const string FinishTournamentMethod = "FinishTournament";
        public const string GetTournamentMethod = "GetTournament";
        public const string GetAllTournamentMethod = "GetAllTournament";

        // battle pass
        public const string GetProfileBattlePassStatesMethod = "GetPlayerBattlePassStates";
        public const string GetBattlePassFullInformationMethod = "GetBattlePassFullInformation";
        public const string AddBattlePassExpirienceMethod = "AddBattlePassExpirience";
        public const string GetRewardFromBattlePassInstanceMethod = "GetRewardFromBattlePassInstance";
        public const string GrantPremiumAccessToBattlePassMethod = "GrantPremiumAccessToBattlePass";
        public const string ResetPlayerStateForBattlePassMethod = "ResetPlayerStateForBattlePass";
        public const string StartBattlePassInstanceMethod = "StartBattlePassInstance";
        public const string StopBattlePassInstanceMethod = "StopBattlePassInstance";
        public const string PreTicketPurchaseProccessMethod = "PreTicketPurchaseProccess";
        public const string PostTicketPurchaseProccessMethod = "PostTicketPurchaseProccess";
        public const string GrantTicketMethod = "GrantTicket";
        public const string GetBattlePassTasksMethod = "GetBattlePassTasks";
        public const string AddBattlePassTaskPointsMethod = "AddBattlePassTasksPoints";
        public const string PickupBattlePassTaskRewardMethod = "PickupBattlePassTaskReward";

        // ban
        public const string BanProfileMethod = "BanProfile";
        public const string RevokeBanProfileMethod = "RevokeBanProfile";
        public const string RevokeAllBansProfileMethod = "RevokeAllBansProfile";
        public const string GetProfileBansMethod = "GetProfileBans";

        // profile avatars
        public const string GetProfileAvatarTableMethod = "GetProfileAvatarTable";
        public const string GetProfileAvatarTableWithStatesMethod = "GetProfileAvatarTableWithStates";
        public const string GetProfileAvatarIDMethod = "GetProfileAvatarID";

        // auth
        public const string PostAuthMethod = "GetPostAuthData";

        // iap
        public const string ValidateIAPPurchaseMethod = "ValidateIAPPurchase";
        public const string PostIAPProcessMethod = "PostIAPProcess";

        // items
        public const string FetchItemsMethod = "FetchItems";
        public const string GetItemsCategoriesMethod = "GetItemsCategories";
        public const string GetFabItemsMethod = "GetFabItems";
        public const string GetFabItemByIDMethod = "GetFabItemByID";
        public const string PostPurchaseProccessMethod = "PostPurchaseProccess";
        public const string GrantItemsToProfileMethod = "GrantItemsToProfile";
        public const string ModifyItemUsesMethod = "ModifyItemUses";
        public const string ConsumeItemMethod = "ConsumeItemUses";

        // inventory
        public const string GetProfileInventoryMethod = "GetProfileInventory";
        public const string GetItemByInventoryIDMethod = "GetItemByInventoryID";
        public const string GetProfileLootboxesMethod = "GetProfileLootboxes";
        public const string GetLootboxesBadgeMethod = "GetLootboxesBadge";
        public const string SetItemEquipStateMethod = "SetItemEquipState";
        public const string UpdateInventoryItemDataMethod = "UpdateInvertoryItemDataByKey";
        public const string RemoveInventoryItemsFromProfileMethod = "RemoveInventoryItemsFromProfile";
        public const string UnlockContainerMethod = "UnlockContainer";
        public const string UnlockLootboxTimerMethod = "UnlockLootboxTimerContainer";

        // crafting
        public const string GetRecipeDependencyStateMethod = "GetRecipeDependencyState";
        public const string CraftItemFromRecipeMethod = "CraftItemFromRecipe";
        public const string CraftItemWithoutRecipeMethod = "CraftItemWithoutRecipe";
        public const string GetItemNextUpgradeStateMethod = "GetItemNextUpgradeState";
        public const string UpgradeItemWithNextStateMethod = "UpgradeItemWithNextState";

        // store
        public const string GetAllStoresMethod = "GetAllStores";
        public const string GetAllStoreTitlesMethod = "GetAllStoreTitles";
        public const string GetStoreByIDMethod = "GetStoreByID";
        public const string GetStoreItemByIDMethod = "GetStoreItemByID";
        public const string PostStorePurchaseProccessMethod = "PostStorePurchaseProccess";
        public const string PreStorePurchaseProccessMethod = "PreStorePurchaseProccess";
        public const string RevokeStoreItemLimitationMethod = "RevokeStoreItemLimitation";
        public const string StartSpecialOfferMethod = "StartSpecialOffer";
        public const string StopSpecialOfferMethod = "StopSpecialOffer";
        public const string GetSpecialOffersMethod = "GetSpecialOffers";
        public const string GrantSpecialOfferToProfileMethod = "GrantSpecialOfferToProfile";

        // friends
        public const string SendFriendRequestMethod = "SendFriendRequest";
        public const string GetFriendsListMethod = "GetFriendsList";
        public const string GetRequestedFriendsListMethod = "GetRequestedFriendsList";
        public const string AcceptFriendRequestMethod = "AcceptFriendRequest";
        public const string DeclineFriendRequestMethod = "DeclineFriendRequest";
        public const string GetFriendsBadgeMethod = "GetFriendsBadge";
        public const string ForceAddFriendMethod = "ForceAddFriend";
        public const string RemoveFriendMethod = "RemoveFriend";
        public const string CheckFriendshipMethod = "CheckFriendship";
        public const string GetSharedFriendsMethod = "GetSharedFriends";

        // leaderboard
        public const string GetLeaderboardMethod = "GetLeaderboard";
        public const string GetLeaderboardAroundProfileMethod = "GetLeaderboardAroundPlayer";
        public const string GetFriendsLeaderboardMethod = "GetFriendsLeaderboard";
        public const string UpdateStatisticMethod = "UpdateStatisticPoint";
        public const string AddStatisticMethod = "AddStatisticPoint";
        public const string ResetProfileStatisticsMethod = "ResetPlayerStatistics";
        public const string GetClanLeaderboardMethod = "GetClansLeaderboard";
        public const string GetLeaderboardAroundClanMethod = "GetLeaderboardAroundClan";
        public const string AddClanStatisticMethod = "AddClanStatisticPoint";
        public const string UpdateClanStatisticMethod = "UpdateClanStatisticPoint";

        // achievements
        public const string GetProfileAchievementsMethod = "GetProfileAchievements";
        public const string AddAchievementPointsMethod = "AddAchievementPoints";
        public const string PickupAchievementRewardMethod = "PickupAchievementReward";
        public const string ResetAchievementMethod = "ResetAchievement";
        public const string GetAchievementsBadgeMethod = "GetAchievementsBadge";

        // profile tasks
        public const string GetProfileTasksPoolMethod = "GetProfileTasksPool";
        public const string GetTasksForProfileMethod = "GetTasksForProfile";
        public const string AddProfileTaskPointsMethod = "AddProfileTaskPoints";
        public const string AddProfileMuliplyTaskPointsMethod = "AddProfileMuliplyTaskPoints";
        public const string PickupProfileTaskRewardMethod = "PickupProfileTaskReward";
        public const string ResetProfileTasksMethod = "ResetProfileTasks";
        public const string GetProfileTasksBadgeMethod = "GetProfileTasksBadge";

        // calendar
        public const string PickupCalendarRewardMethod = "PickupCalendarReward";
        public const string ResetCalendarMethod = "ResetCalendar";
        public const string GetCalendarMethod = "GetCalendar";
        public const string GetAllCalendarsMethod = "GetAllCalendars";
        public const string GetCalendarBadgeMethod = "GetCalendarBadge";
        public const string GrantCalendarMethod = "GrantCalendar";
        public const string PreCalendarPurchaseProccessMethod = "PreCalendarPurchaseProccess";

        // roulette
        public const string GetRouletteTableMethod = "GetRouletteTable";
        public const string SpinRouletteMethod = "SpinRoulette";

        // matchmaking
        public const string GetMatchmakingListMethod = "GetMatchmakingList";
        public const string UpdateMatchmakingQueueMethod = "UpdateMatchmakingQueue";
        public const string RemoveMatchmakingQueueMethod = "RemoveMatchmakingQueue";
        public const string GetMatchmakingQueueMethod = "GetMatchmakingQueue";
        public const string GetMatchMethod = "GetMatch";

        // durable tasks
        public const string StartDurableTaskMethod = "StartDurableTask";
        public const string DurableContextProcessMethod = "DurableContextProcess";
        public const string DurabaleTaskEndMethod = "DurabaleTaskEnd";

        // title data
        public const string GetAllTitleDataMethod = "GetAllTitleData";
        public const string GetTitleDataByKeyMethod = "GetTitleDataByKey";

        // notifications
        public const string SendNotificationMethod = "SendNotification";
        public const string GetNotificationsMethod = "GetNotifications";
        public const string ReadNotificationMethod = "ReadNotification";
        public const string GetNotificationBadgeMethod = "GetNotificationBadge";
        public const string SendNotificationToProfileMethod = "SendNotificationToProfile";
        public const string ClaimNotificationRewardMethod = "ClaimNotificationReward";
        public const string RemoveNotificationMethod = "RemoveNotificationReward";

        // chat
        public const string GetModeratorsListMethod = "GetModeratorsList";
        public const string AddToModeratorListMethod = "AddToModeratorList";
        public const string RemoveFromModeratorListMethod = "RemoveFromModeratorList";
        public const string ClearDialogBadgeMethod = "ClearDialogBadge";
        public const string GetMessagesFromChatMethod = "GetMessagesFromChat";
        public const string SendMessageToChatMethod = "SendMessageToChat";
        public const string ModifyChatMessageMethod = "ModifyChatMessage";
        public const string DeleteChatMessageMethod = "DeleteChatMessage";
        public const string BanProfileInChatMethod = "BanProfileInChat";
        public const string GetChatStickersPackMethod = "GetChatStickersPack";
        public const string ClaimItemFormMessageMethod = "ClaimItemFormMessage";
        public const string GetDialogListMethod = "GetDialogList";
        public const string GetDialogBadgeMethod = "GetDialogDialogBadge";

        // events
        public const string GetEventQueueContainerMethod = "GetEventQueueContainer";
        public const string GetCBSEventsMethod = "GetCBSEventsMethod";
        public const string GetCBSEventByIDMethod = "GetCBSEventByID";
        public const string GetEventBadgeMethod = "GetEventBadge";
        public const string StartCBSEventHandlerMethod = "StartCBSEventHandle";
        public const string StopCBSEventHandlerMethod = "StopCBSEventHandle";
        public const string ExecuteCBSEventHandlerMethod = "ExecuteCBSEventHandle";
        public const string RevokeCBSEventHandlerMethod = "RevokeCBSEventHandle";
        public const string GetCBSEventsLogListMethod = "GetCBSEventsLogList";

        // clan
        public const string CreateClanMethod = "CreateClan";
        public const string FindClanMethod = "FindClan";
        public const string GetClanEntityMethod = "GetClanEntity";
        public const string GetClanOfProfileMethod = "GetClanOfProfile";
        public const string InviteToClanMethod = "InviteToClan";
        public const string GetProfileInvationsMethod = "GetProfileInvations";
        public const string AcceptClanInvationMethod = "AcceptClanInvation";
        public const string DeclineClanInvationMethod = "DeclineClanInvation";
        public const string JoinToClanMethod = "JoinToClan";
        public const string SendClanJoinRequestMethod = "SendClanJoinRequest";
        public const string AcceptClanJoinRequestMethod = "AcceptClanJoinRequest";
        public const string DeclineClanJoinRequestMethod = "DeclineClanJoinRequest";
        public const string GetClanJoinRequestListMethod = "GetClanJoinRequestList";
        public const string LeaveClanMethod = "LeaveClan";
        public const string GetClanFullInformationMethod = "GetClanFullInformation";
        public const string UpdateClanDisplayNameMethod = "UpdateClanDisplayName";
        public const string UpdateClanDescriptionMethod = "UpdateClanDescription";
        public const string UpdateClanVisibilityMethod = "UpdateClanVisibility";
        public const string UpdateClanAvatarMethod = "UpdateClanAvatar";
        public const string UpdateClanCustomDataMethod = "UpdateClanCustomData";
        public const string GetClanCustomDataMethod = "GetClanCustomData";
        public const string GetClanBadgeMethod = "GetClanBadge";
        public const string GetClanMembersMethod = "GetClanMembers";
        public const string KickClanMemberMethod = "KickClanMember";
        public const string ChangeClanMemberRoleMethod = "ChangeClanMemberRole";
        public const string DisbandClanMethod = "DisbandClan";

        // clan economy
        public const string GetClanInventoryMethod = "GetClanInventory";
        public const string GrantItemsToClanMethod = "GrantItemsToClan";
        public const string GetClanCurrencyMethod = "GetClanVirtualCurrency";
        public const string AddClanCurrencyMethod = "AddClanVirtualCurrency";
        public const string SubtractClanCurrencyMethod = "SubtractClanVirtualCurrency";
        public const string TransferItemFromProfileToClanMethod = "TransferItemFromProfileToClan";
        public const string TransferItemFromClanToProfileMethod = "TransferItemFromClanToProfile";

        // clan expirience
        public const string AddExperienceToClanMethod = "AddExperienceToClan";
        public const string GetClanExperienceMethod = "GetClanExperience";

        // clan tasks
        public const string GetTasksForClanMethod = "GetTasksForClan";
        public const string AddClanTaskPointsMethod = "AddClanTaskPoints";
        public const string ResetClanTasksMethod = "ResetClanTasks";

        // health
        public const string CheckHealthMethod = "CheckHealth";

        // example
        public const string ExampleTestMethod = "ExampleTest";


        // All methods list
        public static List<string> AllMethods
        {
            get
            {
                var externalMethodList = new List<string>();
#if UNITY_EDITOR
                var playFabConfigData =  Scriptable.CBSScriptable.Get<Scriptable.PlayFabConfigData>();
                if (!string.IsNullOrEmpty(playFabConfigData.ExternalClassToRegisterAzureFunction))
                {
                    var externalType = Type.GetType(playFabConfigData.ExternalClassToRegisterAzureFunction);
                    if (externalType != null)
                    {
                        externalMethodList = externalType.GetAllPublicConstantValues<string>();
                    }
                }
#endif
                return typeof(AzureFunctions).GetAllPublicConstantValues<string>().Concat(externalMethodList).ToList();
            }
        }

        private static List<T> GetAllPublicConstantValues<T>(this Type type)
        {
            return type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(T))
                .Select(x => (T)x.GetRawConstantValue())
                .ToList();
        }

        public static string GetFunctionFullURL(string funtionUrl, string functionName, string functionMasterKey)
        {
            return string.Format("{0}/api/{1}?code={2}", funtionUrl, functionName, functionMasterKey);
        }
    }
}
