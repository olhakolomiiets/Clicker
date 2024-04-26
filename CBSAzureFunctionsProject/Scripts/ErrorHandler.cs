using System;
using PlayFab;
using CBS.Models;
using PlayFab.ServerModels;

namespace CBS
{
    public static class ErrorHandler
    {
        public static dynamic ThrowError<T>(ErrorCode code, string message) where T :class
        {
            return new ExecuteResult<T>{
                Error = new CBSError{
                    FabCode = PlayFabErrorCode.Unknown,
                    CBSCode = code,
                    Message = message
                }
            };
        }

        public static dynamic ThrowError<T>(PlayFabError error) where T :class
        {
            return new ExecuteResult<T>{
                Error = CBSError.FromTemplate(error)
            };
        }

        public static dynamic ThrowError<T>(ExecuteResult<T> error) where T : ExecuteResult<T>
        {
            return error;
        }

        public static dynamic ThrowError<T>(CBSError error) where T :class
        {
            return new ExecuteResult<T>{
                Error = error
            };
        }

        public static dynamic ThrowError<T>(BaseErrorResult error) where T :class
        {
            return new ExecuteResult<T>{
                Error = error.Error
            };
        }

        public static BaseErrorResult ThrowError(CBSError error)
        {
            return new BaseErrorResult{
                Error = error
            };
        }

        public static BaseErrorResult ThrowError(BaseErrorResult error)
        {
            return error;
        }

        public static dynamic ThrowTableError<T>(Exception error) where T :class
        {
            return new ExecuteResult<T>{
                Error = new CBSError{
                    FabCode = PlayFabErrorCode.Unknown,
                    CBSCode = ErrorCode.COSMOS_TABLE_ERROR,
                    Message = error.Message
                }
            };
        }

        // custom errors
        public static dynamic ThrowGrantItemsToPlayerError<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.FAILED_TO_GRANT_ITEMS, "Failed to grant items");
        }

        public static dynamic LevelTableNotConfiguratedError<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.LEVEL_TABLE_NOT_FOUND, "Level table not found");
        }

        public static dynamic AvatarTableNotConfiguratedError<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.AVATAR_TABLE_NOT_FOUND, "Avatar table not found");
        }

        public static dynamic AvatarNotFoundError<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.AVATAR_NOT_FOUND, "Avatar not found");
        }

        public static dynamic AvatarNotAvailableError<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.AVATAR_NOT_AVAILABLE, "Avatar not available for profile");
        }

        public static dynamic AvatarHasNoPriceError<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.AVATAR_HAS_NO_PRICE, "Avatar has no price");
        }

        public static dynamic AlreadyPurchasedError<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.ALREADY_PURCHASED, "Item already purchased");
        }

        public static dynamic InsufficientFundsError<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.INSUFFICIENT_FUNDS, "Insufficient funds");
        }

        public static dynamic CurrencyPackNotFoundError<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.ITEM_NOT_FOUND, "Currency pack not found");
        }

        public static dynamic CatalogItemNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.ITEM_NOT_FOUND, "Catalog item not found");
        }

        public static dynamic RecipeItemNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.RECIPE_NOT_FOUND, "Recipe not found in database");
        }

        public static dynamic ItemInstanceNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.ITEM_INSTANCE_NOT_FOUND, "Item not found in inventory");
        }

        public static dynamic ItemIsNotUpgradable<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.ITEM_NOT_UPGRADABLE, "Upgrate data for this item not found");
        }

        public static dynamic MaxUpgradeReached<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.MAX_UPGRADE_REACHED, "Max upgrade reached");
        }

        public static dynamic StoreNotEnabled<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.STORE_NOT_ENBALED, "Store not enabled");
        }

        public static dynamic StoreItemNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.STORE_ITEM_NOT_FOUND, "Store Item Not Found");
        }

        public static dynamic StoreItemNotAvailable<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.STORE_ITEM_NOT_AVAILABLE, "Store Item Not Available");
        }

        public static dynamic InvalidInput<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.INVALID_INPUT, "Invalid Inputs");
        }

        public static dynamic TaskAlreadyRunning<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TASK_ALREADY_RUNNING, "Task already running");
        }

        public static dynamic TaskAlreadyStopped<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TASK_ALREADY_STOPPED, "Task already stopped");
        }

        public static dynamic OfferAlreadyGranted<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.OFFER_ALREADY_GRANTRED, "Offer Already Granted");
        }

        public static dynamic FriendsLimitReached<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.FRIENDS_LIMIT_REACHED, "Friend limit reached");
        }

        public static dynamic TaskIDNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TASK_ID_NOT_FOUND, "Task id not found");
        }

        public static dynamic TaskAlreadyRewarded<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TASK_ALREADY_REWARDED, "Task Already Rewarded");
        }

        public static dynamic TaskAlreadyCompleted<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TASK_ALREADY_COMPLETED, "Task Already Completed");
        }

        public static dynamic TaskNotComplete<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TASK_NOT_COMPLETE, "Task Not Complete");
        }

        public static dynamic TasksPoolNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TASKS_POOL_NOT_FOUND, "Tasks Pool Not Found");
        }

        public static dynamic TasksPoolNotConfigured<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TASKS_POOL_NOT_CONFIGURED, "Tasks Pool Not Configured");
        }

        public static dynamic CalendarNotConfigured<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.CALENDAR_NOT_CONFIGURED, "Calendar Not Configured");
        }

        public static dynamic CalendarNotEnabled<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.CALENDAR_NOT_ENABLED, "Calendar Not Enabled");
        }

        public static dynamic InvalidIndex<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.INVALID_INDEX, "Invalid Index");
        }

        public static dynamic AlreadyRewarded<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.ALREADY_REWARDED, "Already Rewarded");
        }

        public static dynamic RewardNotAvailable<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.REWARD_NOT_AVAILAVLE, "Reward Not Available");
        }

        public static dynamic RewardNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.REWARD_NOT_FOUND, "Reward Not Found");
        }

        public static dynamic CalendarNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.CALENDAR_NOT_FOUND, "Calendar Not Found");
        }

        public static dynamic CalendarCanNotBePurchased<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.CALENDAR_CANNOT_BE_PURCHASED, "Calendar Can Not Be Purchased");
        }

        public static dynamic CalendarAlreadyBePurchased<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.CALENDAR_ALREADY_PURCHASED, "Calendar Already Purchased");
        }

        public static dynamic RouletteNotConfigured<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.ROULETTE_NOT_CONFIGURED, "Roulette Not Configured");
        }

        public static dynamic BattlePassNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.BATTLE_PASS_NOT_FOUND, "Battle Pass Not Found");
        }

        public static dynamic BattlePassNotActive<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.BATTLE_PASS_NOT_ACTIVE, "Battle Pass Not Active");
        }

        public static dynamic TicketNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TICKET_NOT_FOUND, "Ticket Not Found");
        }

        public static dynamic TasksNotAvalilable<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TASKS_NOT_AVAILABLE, "Tasks not available");
        }

        public static dynamic TitleDataNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TITLE_DATA_NOT_FOUND, "Title Data not found");
        }

        public static dynamic NotificationNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.NOTIFICATION_NOT_FOUND, "Notification Not Found");
        }

        public static dynamic ChatBan<T>(DateTime bannedUntil) where T : class
        {
            return ThrowError<T>(ErrorCode.CHAT_BAN, "Profile banned from chat until "+bannedUntil.ToString());
        }

        public static dynamic StickerNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.STICKER_NOT_FOUND, "Sticker Not Found");
        }

        public static dynamic ActionBlocked<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.ACTION_BLOCKED, "You do not have sufficient permissions for this action");
        }

        public static dynamic ItemInstanceNotAvailable<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.STORE_ITEM_NOT_AVAILABLE, "Item not available");
        }

        public static dynamic DialogNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.DIALOG_NOT_FOUND, "Dialog not found");
        }

        public static dynamic EventNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.EVENT_NOT_FOUND, "Event not found");
        }

        public static dynamic EventAlreadyRunning<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.EVENT_ALREADY_RUNNING, "Event already running");
        }

        public static dynamic EventAlreadyStoped<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.EVENT_ALREADY_STOPPED, "Event already stopped");
        }

        public static dynamic DisplayNameNotAvailable<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.DISPLAY_NAME_NOT_AVAILABLE, "Display Name not available");
        }

        public static dynamic ClanNotFound<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.CLAN_NOT_FOUND, "Clan not found");
        }

        public static dynamic ProfileIsNotMemberOfClan<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.PROFILE_IS_NOT_MEMBER_OF_CLAN, "Profile is not member of clan");
        }

        public static dynamic NotEnoughRights<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.NOT_ENOUGH_RIGHTS, "The profile does not have sufficient permissions for this action");
        }

        public static dynamic MaxMembersReached<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.NOT_ENOUGH_RIGHTS, "The clan has reached the maximum number of members");
        }

        public static dynamic ClanIsClosed<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.CLAN_IS_CLOSED, "The clan is closed");
        }

        public static dynamic PlayFabTitleIDIsNotConfigured<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.TITLE_ID_NOT_FOUND, "PlayFab TitleID is not configured");
        }

        public static dynamic PlayFabSecretKeyIsNotConfigured<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.SECRET_KEY_NOT_FOUND, "PlayFab Secret key is not configured");
        }

        public static dynamic AzureConnectionStringNotConfigured<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.STORAGE_CONNECTION_STRING_NOT_FOUND, "Azure connection string is not configured");
        }

        public static dynamic AzureKeyNotConfigured<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.AZURE_KEY_NOT_FOUND, "Azure key is not configured");
        }

        public static dynamic FunctionURLNotConfigured<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.FUNCTION_URL_NOT_FOUND, "Function URL is not configured");
        }

        public static dynamic LootboxNotAvalilable<T>() where T : class
        {
            return ThrowError<T>(ErrorCode.LOOTBOX_NOT_AVAILABLE, "Lootbox not available");
        }
    }
}