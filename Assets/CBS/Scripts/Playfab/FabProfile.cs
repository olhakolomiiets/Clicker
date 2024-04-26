using CBS.Models;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;
using System.Collections.Generic;

namespace CBS.Playfab
{
    public class FabProfile : FabExecuter, IFabProfile
    {

        public void GetUserAccountInfo(string playerID, Action<GetAccountInfoResult> onGet, Action<PlayFabError> onFailed)
        {
            GetAccountInfoRequest request = new GetAccountInfoRequest { PlayFabId = playerID };
            PlayFabClientAPI.GetAccountInfo(request, onGet, onFailed);
        }

        public void UpdateUserDisplayName(string profileID, string displayName, bool profanityCheck, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateProfileDisplayNameMethod,
                FunctionParameter = new FunctionUpdateDisplayNameRequest
                {
                    ProfileID = profileID,
                    DisplayName = displayName,
                    ProfanityCheck = profanityCheck
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void AddPlayerExpirience(string profileID, int expToAdd, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddExperienceToPlayerMethod,
                FunctionParameter = new FunctionAddExpRequest
                {
                    ProfileID = profileID,
                    ExpToAdd = expToAdd
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onAdd, onFailed);
        }

        public void GetProfileExpirienceDetail(string profileID, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileExperienceMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void GetProfileDetail(string profileID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileDetailMethod,
                FunctionParameter = new FunctionGetProfileDetailRequest
                {
                    ProfileID = profileID,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetProfileDetailByDisplayName(string displayName, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileDetailByDisplayNameMethod,
                FunctionParameter = new FunctionGetProfileDetailRequest
                {
                    DisplayName = displayName,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetProfilesDetails(string[] profilesIDs, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfilesDetailsMethod,
                FunctionParameter = new FunctionGetProfilesDetailsRequest
                {
                    ProfilesIDs = profilesIDs,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetLevelTable(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileLevelTableMethod,
                FunctionParameter = new FunctionKeyRequest
                {
                    ProfileID = profileID,
                    Key = TitleKeys.LevelTitleDataKey
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void SetAvatarUrl(string profileID, string imageUrl, Action<ExecuteFunctionResult> onSet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateProfileImageURLMethod,
                FunctionParameter = new FunctionUpdateURLRequest
                {
                    ProfileID = profileID,
                    URL = imageUrl
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onSet, onFailed);
        }

        public void SetProfileData(string profileID, string key, string value, Action<ExecuteFunctionResult> onSet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.SetProfilesDataMethod,
                FunctionParameter = new FunctionKeyPairRequest
                {
                    ProfileID = profileID,
                    Key = key,
                    Value = value
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onSet, onFailed);
        }
        
        public void SetProfileData(string profileID, Dictionary<string, string> dataSet, Action<ExecuteFunctionResult> onSet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.SetProfilesMultiplyDataMethod,
                FunctionParameter = new FunctionDictionaryRequest
                {
                    ProfileID = profileID,
                    Dictionary = dataSet
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onSet, onFailed);
        }

        public void GetProfileData(string profileID, string[] keys, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfilesDataMethod,
                FunctionParameter = new FunctionKeysRequest
                {
                    ProfileID = profileID,
                    Keys = keys
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetCombinedAccountInfo(string profileID, Action<GetAccountInfoResult> onSuccess, Action<PlayFabError> onFailed)
        {
            var request = new GetAccountInfoRequest
            {
                PlayFabId = profileID
            };
            PlayFabClientAPI.GetAccountInfo(request, onSuccess, onFailed);
        }

        public void UpdateProfileOnlineState(string profileID, int onlineThreshold, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateProfileOnlineStateMethod,
                FunctionParameter = new FunctionUpdateOnlineRequest
                {
                    ProfileID = profileID,
                    OnlineThreshold = onlineThreshold
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void BanProfile(string profileID, string reason, uint hours, Action<ExecuteFunctionResult> onBan, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.BanProfileMethod,
                FunctionParameter = new FunctionBanProfileRequest
                {
                    ProfileID = profileID,
                    Reason = reason,
                    Hours = hours
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onBan, onFailed);
        }

        public void RevokeAllBans(string profileID, Action<ExecuteFunctionResult> onBan, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.RevokeAllBansProfileMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onBan, onFailed);
        }

        public void RevokeProfileBan(string banID, Action<ExecuteFunctionResult> onBan, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.RevokeBanProfileMethod,
                FunctionParameter = new FunctionRevokeBanRequest
                {
                    BanID = banID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onBan, onFailed);
        }

        public void GetProfileBans(string profileID, Action<ExecuteFunctionResult> onBan, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileBansMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onBan, onFailed);
        }

        public void UpdateProfileAvatarID(string profileID, string avatarID, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateProfileAvatarIDMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = avatarID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void PurchaseAvatar(string profileID, string avatarID, Action<ExecuteFunctionResult> onPurchase, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PurchaseProfileAvatarMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = avatarID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onPurchase, onFailed);
        }

        public void GrantAvatar(string profileID, string avatarID, Action<ExecuteFunctionResult> onGrant, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GrantProfileAvatarMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = avatarID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGrant, onFailed);
        }

        public void GetProfileAvatarID(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileAvatarIDMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetProfileAvatarTable(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileAvatarTableMethod,
                FunctionParameter = new FunctionBaseRequest { }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetProfileAvatarTableWithStates(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileAvatarTableWithStatesMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }
        
        public void DeleteMasterPlayerAccount(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.DeleteMasterPlayerAccountMethod,
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }
    }
}