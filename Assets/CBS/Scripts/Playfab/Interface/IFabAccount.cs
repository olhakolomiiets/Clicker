using CBS.Models;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;
using System.Collections.Generic;

namespace CBS.Playfab
{
    public interface IFabProfile
    {
        void GetUserAccountInfo(string playerID, Action<GetAccountInfoResult> onGet, Action<PlayFabError> onFailed);

        void UpdateUserDisplayName(string profileID, string displayName, bool profanityCheck,
            Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void AddPlayerExpirience(string profileID, int newExp, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void GetProfileExpirienceDetail(string profileID, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void GetProfileDetail(string profileID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetProfileDetailByDisplayName(string displayName, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void SetAvatarUrl(string profileID, string imageUrl, Action<ExecuteFunctionResult> onSet, Action<PlayFabError> onFailed);

        void SetProfileData(string profileID, string key, string value, Action<ExecuteFunctionResult> onSet, Action<PlayFabError> onFailed);

        void SetProfileData(string profileID, Dictionary<string, string> dataSet, Action<ExecuteFunctionResult> onSet, Action<PlayFabError> onFailed);

        void GetProfileData(string profileID, string[] keys, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetCombinedAccountInfo(string profileID, Action<GetAccountInfoResult> onSuccess, Action<PlayFabError> onFailed);

        void GetLevelTable(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetProfilesDetails(string[] profilesIDs, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void UpdateProfileOnlineState(string profileID, int onlineThreshold, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void BanProfile(string profileID, string reason, uint hours, Action<ExecuteFunctionResult> onBan, Action<PlayFabError> onFailed);

        void RevokeAllBans(string profileID, Action<ExecuteFunctionResult> onBan, Action<PlayFabError> onFailed);

        void RevokeProfileBan(string banID, Action<ExecuteFunctionResult> onBan, Action<PlayFabError> onFailed);

        void GetProfileBans(string profileID, Action<ExecuteFunctionResult> onBan, Action<PlayFabError> onFailed);

        void UpdateProfileAvatarID(string profileID, string avatarID, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void PurchaseAvatar(string profileID, string avatarID, Action<ExecuteFunctionResult> onPurchase, Action<PlayFabError> onFailed);

        void GrantAvatar(string profileID, string avatarID, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed);

        void GetProfileAvatarID(string profileID, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void GetProfileAvatarTable(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetProfileAvatarTableWithStates(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void DeleteMasterPlayerAccount(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
    }
}
