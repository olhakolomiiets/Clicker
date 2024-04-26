using CBS.Models;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;

namespace CBS
{
    public interface IProfile
    {
        /// <summary>
        /// An event that reports when the username has been updated.
        /// </summary>
        event Action<CBSUpdateDisplayNameResult> OnDisplayNameUpdated;
        /// <summary>
        /// An event that reports when information about the current user has been received.
        /// </summary>
        event Action<CBSGetAccountInfoResult> OnAcountInfoGetted;
        /// <summary>
        /// An event that reports when the current player's experience points have been changed.
        /// </summary>
        event Action<CBSLevelDataResult> OnPlayerExperienceUpdated;
        /// <summary>
        /// An event that reports when the profile avatar has been updated.
        /// </summary>
        event Action<AvatarInfo> OnAvatarUpdated;
        /// <summary>
        /// An event that reports when the linked acoounts state have been changed.
        /// </summary>
        event Action<AccountLinkedInfo> OnLinkedAccountInfoChanged;

        /// <summary>
        /// Unique user identifier.
        /// </summary>
        string ProfileID { get; }

        /// <summary>
        /// Display name of current user.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Profile clna ID is exsit
        /// </summary>
        string ClanID { get; }

        /// <summary>
        /// Registration date of the current user.
        /// </summary>
        DateTime RegistrationDate { get; }

        /// <summary>
        /// Entity ID of current user. Used by Playfab for new features such as groups for example.
        /// </summary>
        string EntityID { get; }

        /// <summary>
        /// Entity type of current user. For profile its always "title_player_account".
        /// </summary>
        string EntityType { get; }

        /// <summary>
        /// Cached Entity key.
        /// </summary>
        EntityKey EntityKey { get; }

        /// <summary>
        /// Last cached user level information.
        /// </summary>
        LevelInfo CachedLevelInfo { get; }

        /// <summary>
        /// Last cached profile data
        /// </summary>
        Dictionary<string, UserDataRecord> CachedProfileData { get; }

        /// <summary>
        /// Information about linked accounts.
        /// </summary>
        AccountLinkedInfo LinkedAccounts { get; }

        /// <summary>
        /// Infrormation about player avatar id or image url.
        /// </summary>
        AvatarInfo Avatar { get; }

        /// <summary>
        /// Check if profile exist if clan
        /// </summary>
        bool ExistInClan { get; }

        /// <summary>
        /// Clan entity of joined clan
        /// </summary>
        ClanEntity ClanEntity { get; }

        /// <summary>
        /// Profile session context
        /// </summary>
        PlayFabAuthenticationContext AuthenticationContex { get; }

        /// <summary>
        /// Update player display name
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="result"></param>
        void UpdateDisplayName(string userName, Action<CBSUpdateDisplayNameResult> result = null);

        /// <summary>
        /// Get full information of current player account. Include all Playfab origin result.
        /// </summary>
        /// <param name="result"></param>
        void GetAccountInfo(Action<CBSGetAccountInfoResult> result);

        /// <summary>
        /// Get full information of player account by id. Include all Playfab origin result.
        /// </summary>
        /// <param name="result"></param>
        void GetProfileAccountInfo(string userID, Action<CBSGetAccountInfoResult> result);

        /// <summary>
        /// Adds N points of experience to the current state. In the response, you can get information whether the player has reached a new level and also information about the reward about the new level.
        /// </summary>
        /// <param name="expToAdd"></param>
        /// <param name="result"></param>
        void AddExpirienceToProfile(int expToAdd, Action<CBSLevelDataResult> result = null);

        /// <summary>
        /// Get information about current experience/level of current profile
        /// </summary>
        /// <param name="result"></param>
        void GetProfileLevelDetail(Action<CBSLevelDataResult> result);

        /// <summary>
        /// Get information about current experience/level of profile
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void GetProfileLevelDetail(string profileID, Action<CBSLevelDataResult> result);

        /// <summary>
        /// Get an array with information about all levels in the game.
        /// </summary>
        /// <param name="result"></param>
        void GetLevelTable(Action<CBSGetLevelTableResult> result);

        /// <summary>
        /// Get general game information about a player, including player ID, avatar url, display name, player level and clan information.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void GetProfileDetail(CBSGetProfileRequest request, Action<CBSGetProfileResult> result);

        /// <summary>
        /// Get general game information about a player by display name, including profile ID, avatar url, display name, player level and clan information.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void GetProfileDetailByDisplayName(string displayName, CBSProfileConstraints constraints, Action<CBSGetProfileResult> result);

        /// <summary>
        /// Get general game information about group of players, including profile ID, avatar url, display name, player level and clan information.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void GetProfilesDetails(CBSGetProfilesRequest request, Action<CBSGetProfilesResult> result);

        /// <summary>
        /// Update the current player's profile photo.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="result"></param>
        void UpdateAvatarUrl(string url, Action<CBSUpdateAvatarUrlResult> result);

        /// <summary>
        /// Update the current player avatar id.
        /// </summary>
        /// <param name="avatarID"></param>
        /// <param name="result"></param>
        void UpdateAvatarID(string avatarID, Action<CBSUpdateAvatarIDResult> result);

        /// <summary>
        /// Purchase an avatar if it has a price.
        /// </summary>
        /// <param name="avatarID"></param>
        /// <param name="result"></param>
        void PurchaseAvatar(string avatarID, Action<CBSPurchaseAvatarResult> result);

        /// <summary>
        /// Grant an avatar if it has a price.
        /// </summary>
        /// <param name="avatarID"></param>
        /// <param name="result"></param>
        void GrantAvatar(string avatarID, Action<CBSGrantAvatarResult> result);

        /// <summary>
        /// Get the specific player data of current profile by unique key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="result"></param>
        void GetProfileData(string key, Action<CBSGetProfileDataResult> result);

        /// <summary>
        /// Get the specific player data of current profile by unique keys.
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="result"></param>
        void GetProfileData(string[] keys, Action<CBSGetProfileDataResult> result);

        /// <summary>
        /// Get the custom player data by profile id
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="key"></param>
        /// <param name="result"></param>
        void GetProfileDataByPlayerID(string playerID, string key, Action<CBSGetProfileDataResult> result);

        /// <summary>
        /// Get the custom player data by profile id
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="keys"></param>
        /// <param name="result"></param>
        void GetProfileDataByPlayerID(string playerID, string[] keys, Action<CBSGetProfileDataResult> result);

        /// <summary>
        /// Set/Save custom player data of current profile. 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        void SaveProfileData(string key, string value, Action<CBSBaseResult> result);

        /// <summary>
        /// Set/Save custom player data set of current profile. 
        /// </summary>
        /// <param name="dataSet"></param>
        /// <param name="result"></param>
        void SaveProfileData(Dictionary<string, string> dataSet, Action<CBSBaseResult> result);

        /// <summary>
        /// Get information abount linked accounts of profile
        /// </summary>
        /// <param name="result"></param>
        void GetAccountLinkedInfo(Action<CBSGetAccountLinkedInfoResult> result);

        /// <summary>
        /// Call to update online status manually, you need to call it, then you have selected "OnlineBehavior" as "Custom" in your profile settings.
        /// </summary>
        /// <param name="result"></param>
        void UpdateOnlineState(Action<CBSBaseResult> result = null);

        /// <summary>
        /// Ban the user for a while. During the ban, the player will not be able to log in.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void BanProfile(CBSBanProfileRequest request, Action<CBSBanProfileResult> result);

        /// <summary>
        /// Revoke all active bans from the profile.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void RevokeAllProfileBans(string profileID, Action<CBSBaseResult> result);

        /// <summary>
        /// Revoke individual ban using BanID for profile.
        /// </summary>
        /// <param name="banID"></param>
        /// <param name="result"></param>
        void RevokeProfileBan(string banID, Action<CBSBaseResult> result);

        /// <summary>
        /// Get information about all ban for profile.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void GetProfileBanList(string profileID, Action<CBSBanListResult> result);

        /// <summary>
        /// Get avatar id of current profile
        /// </summary>
        /// <param name="result"></param>
        void GetProfileAvatarID(Action<CBSGetProfileAvatarIDResult> result);

        /// <summary>
        /// Get avatar id by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void GetProfileAvatarID(string profileID, Action<CBSGetProfileAvatarIDResult> result);

        /// <summary>
        /// Get all avatars available for profile
        /// </summary>
        /// <param name="result"></param>
        void GetProfileAvatarTable(Action<CBSGetProfileAvatarTableResult> result);

        /// <summary>
        /// Get all avatars available for profile with states (available, purchased)
        /// </summary>
        /// <param name="result"></param>
        void GetProfileAvatarTableWithStates(Action<CBSGetProfileAvatarTableWithStatesResult> result);

        /// <summary>
        /// Delete master player account and all information associated with this account
        /// </summary>
        /// <param name="result"></param>
        void DeleteMasterPlayerAccount(Action<CBSBaseResult> result);
    }
}
