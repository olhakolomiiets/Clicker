using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;
using System.Collections.Generic;

namespace CBS.Playfab
{
    public class FabClan : FabExecuter, IFabClan
    {
        public void CreateClan(FunctionCreateClanRequest clanRequest, Action<ExecuteFunctionResult> onCreate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.CreateClanMethod,
                FunctionParameter = clanRequest
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onCreate, onFailed);
        }

        public void SearchClan(string clanName, Action<ExecuteFunctionResult> onSearch, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.FindClanMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ID = clanName
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onSearch, onFailed);
        }

        public void GetClanEntity(string clanID, CBSClanConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanEntityMethod,
                FunctionParameter = new GetClanEntityRequest
                {
                    ClanID = clanID,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetClanOfProfile(string profileID, CBSClanConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanOfProfileMethod,
                FunctionParameter = new GetClanEntityRequest
                {
                    ProfileID = profileID,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void InviteToClan(string profileID, string profileIDToInvite, Action<ExecuteFunctionResult> onInvite, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.InviteToClanMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = profileIDToInvite
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onInvite, onFailed);
        }

        public void GetProfileInvations(string profileID, CBSClanConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetProfileInvationsMethod,
                FunctionParameter = new FunctionGetInvationsRequest
                {
                    ProfileID = profileID,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void SendJoinRequest(string profileID, string clanID, Action<ExecuteFunctionResult> onSend, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.SendClanJoinRequestMethod,
                FunctionParameter = new FunctionClanRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onSend, onFailed);
        }

        public void GetJoinRequestList(string clanID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanJoinRequestListMethod,
                FunctionParameter = new FunctionGetClanProfilesRequest
                {
                    ClanID = clanID,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void AcceptJoinRequest(string profileID, string profileIDToAccept, string clanID, Action<ExecuteFunctionResult> onAccept, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AcceptClanJoinRequestMethod,
                FunctionParameter = new FunctionAcceptJoinRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                    ProfileIDToJoin = profileIDToAccept
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onAccept, onFailed);
        }

        public void DeclineJoinRequest(string profileID, string profileIDToDecline, string clanID, Action<ExecuteFunctionResult> onDecline, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.DeclineClanJoinRequestMethod,
                FunctionParameter = new FunctionAcceptJoinRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                    ProfileIDToJoin = profileIDToDecline
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onDecline, onFailed);
        }

        public void GetClanFullInfo(string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanFullInformationMethod,
                FunctionParameter = new FunctionClanRequest
                {
                    ClanID = clanID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void AcceptInvite(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AcceptClanInvationMethod,
                FunctionParameter = new FunctionClanRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void DeclineInvite(string profileID, string clanID, Action<ExecuteFunctionResult> onDecline, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.DeclineClanInvationMethod,
                FunctionParameter = new FunctionClanRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onDecline, onFailed);
        }

        public void JoinToClan(string profileID, string clanID, Action<ExecuteFunctionResult> onJoin, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.JoinToClanMethod,
                FunctionParameter = new FunctionClanRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onJoin, onFailed);
        }

        public void LeaveClan(string profileID, string clanID, Action<ExecuteFunctionResult> onLeave, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.LeaveClanMethod,
                FunctionParameter = new FunctionClanRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onLeave, onFailed);
        }

        public void UpdateDisplayName(string profileID, string clanID, string displayName, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateClanDisplayNameMethod,
                FunctionParameter = new FunctionUpdateClanMetaDataRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                    DisplayName = displayName
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void UpdateDescription(string profileID, string clanID, string description, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateClanDescriptionMethod,
                FunctionParameter = new FunctionUpdateClanMetaDataRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                    Description = description
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void UpdateVisibility(string profileID, string clanID, ClanVisibility visibility, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateClanVisibilityMethod,
                FunctionParameter = new FunctionUpdateClanMetaDataRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                    Visibility = visibility
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void UpdateAvatar(string profileID, string clanID, ClanAvatarInfo avatarInfo, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateClanAvatarMethod,
                FunctionParameter = new FunctionUpdateClanMetaDataRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                    AvatarInfo = avatarInfo
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void UpdateCustomData(string profileID, string clanID, Dictionary<string, string> updateRequest, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateClanCustomDataMethod,
                FunctionParameter = new FunctionUpdateCustomDataRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                    UpdateRequest = updateRequest
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void GetCustomData(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanCustomDataMethod,
                FunctionParameter = new FunctionClanRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetBadge(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanBadgeMethod,
                FunctionParameter = new FunctionBaseRequest
                {
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetClanMembers(string clanID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanMembersMethod,
                FunctionParameter = new FunctionGetClanProfilesRequest
                {
                    ClanID = clanID,
                    Constraints = constraints
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void KickMember(string profileID, string profileIDToKick, string clanID, Action<ExecuteFunctionResult> onKick, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.KickClanMemberMethod,
                FunctionParameter = new FunctionKickClanMemberRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID,
                    ProfileIDToKick = profileIDToKick
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onKick, onFailed);
        }

        public void ChangeMemberRole(string profileID, string profileIDToChange, string clanID, string newRoleID, Action<ExecuteFunctionResult> onChange, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ChangeClanMemberRoleMethod,
                FunctionParameter = new FunctionChangeRoleRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID,
                    ProfileIDToChange = profileIDToChange,
                    NewRoleID = newRoleID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onChange, onFailed);
        }

        public void DisbandClan(string profileID, string clanID, Action<ExecuteFunctionResult> onDisband, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.DisbandClanMethod,
                FunctionParameter = new FunctionClanRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onDisband, onFailed);
        }

        // expirience

        public void AddExpirience(string profileID, string clanID, int expToAdd, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddExperienceToClanMethod,
                FunctionParameter = new FunctionAddClanExpRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID,
                    ExpToAdd = expToAdd
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onAdd, onFailed);
        }

        public void GetLevelInfo(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanExperienceMethod,
                FunctionParameter = new FunctionClanRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        // economy

        public void GetInventory(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanInventoryMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ID = clanID,
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GrantItems(string profileID, string clanID, string[] itemsIDs, bool containPack, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GrantItemsToClanMethod,
                FunctionParameter = new GrantItemsToClanRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID,
                    ItemsIDs = itemsIDs,
                    ContainPack = containPack
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetCurrency(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetClanCurrencyMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ID = clanID,
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void AddCurrency(string profileID, string clanID, string code, int amount, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddClanCurrencyMethod,
                FunctionParameter = new FunctionChangeClanCurrencyRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID,
                    Code = code,
                    Amount = amount
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void SubtractCurrency(string profileID, string clanID, string code, int amount, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.SubtractClanCurrencyMethod,
                FunctionParameter = new FunctionChangeClanCurrencyRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID,
                    Code = code,
                    Amount = amount
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void TransferItemFromProfileToClan(string profileID, string clanID, PlayFabAuthenticationContext auth, string itemInstanceID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.TransferItemFromProfileToClanMethod,
                FunctionParameter = new FunctionClanTransferItemRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID,
                    ProfileAuthContext = auth,
                    ItemInstanceID = itemInstanceID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void TransferItemFromClanToProfile(string profileID, string clanID, PlayFabAuthenticationContext auth, string itemInstanceID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.TransferItemFromClanToProfileMethod,
                FunctionParameter = new FunctionClanTransferItemRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID,
                    ProfileAuthContext = auth,
                    ItemInstanceID = itemInstanceID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetTasksForClan(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetTasksForClanMethod,
                FunctionParameter = new FunctionClanTasksRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void ModifyTasksPoint(string profileID, string clanID, string taskID, int points, ModifyMethod method, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.AddClanTaskPointsMethod,
                FunctionParameter = new FunctionModifyClanTasksPointsRequest
                {
                    ProfileID = profileID,
                    ClanID = clanID,
                    Points = points,
                    Method = method,
                    TaskID = taskID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void ResetTasksForClan(string profileID, string clanID, Action<ExecuteFunctionResult> onReset, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ResetClanTasksMethod,
                FunctionParameter = new FunctionClanTasksRequest
                {
                    ClanID = clanID,
                    ProfileID = profileID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onReset, onFailed);
        }
    }
}
