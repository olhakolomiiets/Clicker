using CBS.Models;
using System;
using System.Collections.Generic;

namespace CBS
{
    public interface IClan : IClanExpirience, IClanEconomy, IClanTasks
    {
        /// <summary>
        /// Notifies when a user has created a clan.
        /// </summary>
        event Action<CBSCreateClanResult> OnCreateClan;
        /// <summary>
        /// Notifies when a user has joined a clan
        /// </summary>
        event Action<CBSJoinToClanResult> OnJoinClan;
        /// <summary>
        /// Notifies when a user has left the clan.
        /// </summary>
        event Action OnLeaveClan;
        /// <summary>
        /// Notifies when a user has deleted a clan.
        /// </summary>
        event Action OnDisbandClan;
        /// <summary>
        /// Notifies when a user has been accepted into a clan.
        /// </summary>
        event Action<CBSAcceptDeclineClanRequestResult> OnProfileAccepted;
        /// <summary>
        /// Notifies when a clan invitation has been declined for a user.
        /// </summary>
        event Action<CBSAcceptDeclineClanRequestResult> OnProfileDeclined;
        /// <summary>
        /// Notifies when a user has decline clan invation
        /// </summary>
        event Action<CBSDeclineInviteResult> OnDeclineInvation;
        /// <summary>
        /// Notifies when a user has accept clan invation
        /// </summary>
        event Action<CBSJoinToClanResult> OnProfileAcceptInvation;

        /// <summary>
        /// Profile clan ID is exsit
        /// </summary>
        string CurrentClanID { get; }
        /// <summary>
        /// Current clan Role ID 
        /// </summary>
        string CurrentRoleID { get; }
        /// <summary>
        /// Check if profile exist if clan
        /// </summary>
        bool ExistInClan { get;  }

        /// <summary>
        /// Clan entity of joined clan
        /// </summary>
        ClanEntity CurrentClanEntity { get; }

        /// <summary>
        /// Creation of a new clan.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void CreateClan(CBSCreateClanRequest request, Action<CBSCreateClanResult> result);
        /// <summary>
        /// Find a clan by name. Full name is required.
        /// </summary>
        /// <param name="clanName"></param>
        /// <param name="result"></param>
        void SearchClanByName(string clanName, Action<CBSGetClanEntityResult> result);
        /// <summary>
        /// Get shot information about clan using constraints.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="constraints"></param>
        /// <param name="result"></param>
        void GetClanEntity(string clanID, CBSClanConstraints constraints, Action<CBSGetClanEntityResult> result);
        /// <summary>
        /// Get profile clan information.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="constraints"></param>
        /// <param name="result"></param>
        void GetClanOfProfile(string profileID, CBSClanConstraints constraints, Action<CBSGetClanEntityResult> result);
        /// <summary>
        /// Get full information about the clan.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void GetClanFullInformation(string clanID, Action<CBSGetClanFullInfoResult> result);
        /// <summary>
        /// Send an application to join the clan.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void SendJoinRequest(string clanID, Action<CBSBaseResult> result);
        /// <summary>
        /// Leave current clan if exist.
        /// </summary>
        /// <param name="result"></param>
        void LeaveClan(Action<CBSBaseResult> result);
        /// <summary>
        /// Disband current clan.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="clanName"></param>
        /// <param name="result"></param>
        void DisbandClan(Action<CBSBaseResult> result);
        /// <summary>
        /// Send an invitation to a profile to join a clan.
        /// </summary>
        /// <param name="profileIDToInvite"></param>
        /// <param name="result"></param>
        void InviteToClan(string profileIDToInvite, Action<CBSInviteToClanResult> result);
        /// <summary>
        /// Get a list of all invitations of the current profile to join the clan.
        /// </summary>
        /// <param name="result"></param>
        void GetProfileInvitations(CBSClanConstraints constraints, Action<CBSGetProfileInvationsResult> result);
        /// <summary>
        /// Decline clan invitation to join.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void DeclineInvite(string clanID, Action<CBSDeclineInviteResult> result);
        /// <summary>
        /// Accept the clan's invitation to join.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void AcceptInvite(string clanID, Action<CBSJoinToClanResult> result);
        /// <summary>
        /// Force join to clan if clan visibility is open
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void JoinToClan(string clanID, Action<CBSJoinToClanResult> result);
        /// <summary>
        /// Update the description of the clan the user is currently a member of
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="description"></param>
        /// <param name="result"></param>
        void UpdateClanDescription(string description, Action<CBSBaseResult> result);
        /// <summary>
        /// Update the avatar of the clan in which the user is currently a member.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="url"></param>
        /// <param name="result"></param>
        void UpdateClanAvatar(ClanAvatarInfo clanAvatarInfo, Action<CBSBaseResult> result);
        /// <summary>
        /// Update the join request type of the clan the user is currently a member of
        /// </summary>
        /// <param name="visibility"></param>
        /// <param name="result"></param>
        void UpdateClanVisibility(ClanVisibility visibility, Action<CBSBaseResult> result);
        /// <summary>
        /// Update the display name of the clan the user is currently a member of
        /// </summary>
        /// <param name="visibility"></param>
        /// <param name="result"></param>
        void UpdateClanDisplayName(string displayName, Action<CBSBaseResult> result);
        /// <summary>
        /// Set clan clan custom data by specific id.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="updateRequest"></param>
        /// <param name="result"></param>
        void UpdateClanCustomData(string clanID, Dictionary<string, string> updateRequest, Action<CBSBaseResult> result);
        /// <summary>
        /// Get clan all custom data.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void GetClanCustomData(string clanID, Action<CBSGetClanCustomDataResult> result);
        /// <summary>
        /// Get a list of all users who want to join the clan.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void GetClanJoinRequestsList(CBSProfileConstraints constraints, Action<CBSGetClanJoinRequestListResult> result);
        /// <summary>
        /// Accept the profile request to join the clan.
        /// </summary>
        /// <param name="profileIDToAccept"></param>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void AcceptProfileJoinRequest(string profileIDToAccept, Action<CBSAcceptDeclineClanRequestResult> result);
        /// <summary>
        /// Decline the profile request to join the clan
        /// </summary>
        /// <param name="profileIDToDecline"></param>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void DeclineProfileJoinRequest(string profileIDToDecline, Action<CBSAcceptDeclineClanRequestResult> result);
        /// <summary>
        /// Get a list of all clan members.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void GetClanMemberships(string clanID, CBSProfileConstraints constraints, Action<CBSGetClanMembersResult> result);
        /// <summary>
        /// Get information about requests/invations count for profile
        /// </summary>
        /// <param name="result"></param>
        void GetClanBadge(Action<CBSGetClanBadgeResult> result);
        /// <summary>
        /// Remove a member from the clan.
        /// </summary>
        /// <param name="userEntityID"></param>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void KickClanMember(string profileIdToKick, Action<CBSBaseResult> result);
        /// <summary>
        /// Change clan member role is profile has permission for this action
        /// </summary>
        /// <param name="memberProfileID"></param>
        /// <param name="newRoleID"></param>
        /// <param name="result"></param>
        void ChangeMemberRole(string memberProfileID, string newRoleID, Action<CBSChangeRoleResult> result);
    }
}
