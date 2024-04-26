using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;
using System.Collections.Generic;

namespace CBS.Playfab
{
    public interface IFabClan
    {
        void CreateClan(FunctionCreateClanRequest clanRequest, Action<ExecuteFunctionResult> onCreate, Action<PlayFabError> onFailed);

        void GetClanFullInfo(string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void DisbandClan(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void AcceptInvite(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void DeclineInvite(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void JoinToClan(string profileID, string clanID, Action<ExecuteFunctionResult> onJoin, Action<PlayFabError> onFailed);

        void GetClanMembers(string clanID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void SearchClan(string clanName, Action<ExecuteFunctionResult> onSearch, Action<PlayFabError> onFailed);

        void GetClanEntity(string clanID, CBSClanConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetClanOfProfile(string profileID, CBSClanConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void InviteToClan(string profileID, string profileIDToInvite, Action<ExecuteFunctionResult> onInvite, Action<PlayFabError> onFailed);

        void GetProfileInvations(string profileID, CBSClanConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void SendJoinRequest(string profileID, string clanID, Action<ExecuteFunctionResult> onSend, Action<PlayFabError> onFailed);

        void GetJoinRequestList(string clanID, CBSProfileConstraints constraints, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void AcceptJoinRequest(string profileID, string profileIDToAccept, string clanID, Action<ExecuteFunctionResult> onAccept, Action<PlayFabError> onFailed);

        void DeclineJoinRequest(string profileID, string profileIDToDecline, string clanID, Action<ExecuteFunctionResult> onDecline, Action<PlayFabError> onFailed);

        void LeaveClan(string profileID, string clanID, Action<ExecuteFunctionResult> onLeave, Action<PlayFabError> onFailed);

        void UpdateDisplayName(string profileID, string clanID, string displayName, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void UpdateDescription(string profileID, string clanID, string description, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void UpdateVisibility(string profileID, string clanID, ClanVisibility visibility, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void UpdateAvatar(string profileID, string clanID, ClanAvatarInfo avatarInfo, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void UpdateCustomData(string profileID, string clanID, Dictionary<string, string> updateRequest, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void GetCustomData(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetBadge(string profileID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void KickMember(string profileID, string profileIDToKick, string clanID, Action<ExecuteFunctionResult> onKick, Action<PlayFabError> onFailed);

        void ChangeMemberRole(string profileID, string profileIDToChange, string clanID, string newRoleID, Action<ExecuteFunctionResult> onChange, Action<PlayFabError> onFailed);

        void AddExpirience(string profileID, string clanID, int expToAdd, Action<ExecuteFunctionResult> onAdd, Action<PlayFabError> onFailed);

        void GetLevelInfo(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetInventory(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GrantItems(string profileID, string clanID, string[] itemsIDs, bool containPack, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetCurrency(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void AddCurrency(string profileID, string clanID, string code, int amount, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void SubtractCurrency(string profileID, string clanID, string code, int amount, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void TransferItemFromProfileToClan(string profileID, string clanID, PlayFabAuthenticationContext auth, string itemInstanceID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void TransferItemFromClanToProfile(string profileID, string clanID, PlayFabAuthenticationContext auth, string itemInstanceID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetTasksForClan(string profileID, string clanID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void ModifyTasksPoint(string profileID, string clanID, string taskID, int points, ModifyMethod method, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void ResetTasksForClan(string profileID, string clanID, Action<ExecuteFunctionResult> onReset, Action<PlayFabError> onFailed);
    }
}
