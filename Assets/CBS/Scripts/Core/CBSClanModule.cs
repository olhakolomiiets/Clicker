using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSClanModule : CBSModule, IClan
    {
        /// <summary>
        /// Notifies when a user has created a clan.
        /// </summary>
        public event Action<CBSCreateClanResult> OnCreateClan;
        /// <summary>
        /// Notifies when a user has joined a clan
        /// </summary>
        public event Action<CBSJoinToClanResult> OnJoinClan;
        /// <summary>
        /// Notifies when a user has left the clan.
        /// </summary>
        public event Action OnLeaveClan;
        /// <summary>
        /// Notifies when a user has deleted a clan.
        /// </summary>
        public event Action OnDisbandClan;
        /// <summary>
        /// Notifies when a user has been accepted into a clan.
        /// </summary>
        public event Action<CBSAcceptDeclineClanRequestResult> OnProfileAccepted;
        /// <summary>
        /// Notifies when a clan invitation has been declined for a user.
        /// </summary>
        public event Action<CBSAcceptDeclineClanRequestResult> OnProfileDeclined;
        /// <summary>
        /// Notifies when a user has decline clan invation
        /// </summary>
        public event Action<CBSDeclineInviteResult> OnDeclineInvation;
        /// <summary>
        /// Notifies when a user has accept clan invation
        /// </summary>
        public event Action<CBSJoinToClanResult> OnProfileAcceptInvation;

        /// <summary>
        /// Profile clan ID is exsit
        /// </summary>
        public string CurrentClanID { get; private set; }
        /// <summary>
        /// Current clan Role ID 
        /// </summary>
        public string CurrentRoleID { get; private set; }
        /// <summary>
        /// Check if profile exist if clan
        /// </summary>
        public bool ExistInClan { get; private set; }

        /// <summary>
        /// Clan entity of joined clan
        /// </summary>
        public ClanEntity CurrentClanEntity { get; private set; }

        private IFabClan FabClan { get; set; }
        private IProfile Profile { get; set; }
        private ICBSItems Items { get; set; }

        protected override void Init()
        {
            FabClan = FabExecuter.Get<FabClan>();
            Profile = Get<CBSProfileModule>();
            Items = Get<CBSItemsModule>();
        }

        // API calls

        /// <summary>
        /// Get full information about the clan.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void GetClanFullInformation(string clanID, Action<CBSGetClanFullInfoResult> result)
        {
            FabClan.GetClanFullInfo(clanID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetClanFullInfoResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<CBSGetClanFullInfoResult>();
                    var info = functionResult.Info;

                    result?.Invoke(new CBSGetClanFullInfoResult
                    {
                        IsSuccess = true,
                        Info = info
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetClanFullInfoResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Creation of a new clan.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void CreateClan(CBSCreateClanRequest request, Action<CBSCreateClanResult> result)
        {
            var profileID = Profile.ProfileID;
            var entityID = Profile.EntityID;

            var createRequest = new FunctionCreateClanRequest
            {
                EntityID = entityID,
                ProfileID = profileID,
                DisplayName = request.DisplayName,
                Description = request.Description,
                AvatarInfo = request.AvatarInfo,
                CustomData = request.CustomData,
                Visibility = request.Visibility
            };

            FabClan.CreateClan(createRequest, onCreate =>
            {
                var cbsError = onCreate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSCreateClanResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onCreate.GetResult<FunctionCreateClanResult>();
                    var clanID = functionResult.ClanID;
                    var groupID = functionResult.GroupID;
                    var clanEntity = functionResult.ClanEntity;
                    var createCallback = new CBSCreateClanResult
                    {
                        IsSuccess = true,
                        ClanID = clanID,
                        GroupID = groupID,
                        ClanEntity = clanEntity
                    };
                    Get<CBSProfileModule>().ParseClanInfo(clanID, clanEntity);
                    Get<CBSClanModule>().ParseClanInfo(clanID, ClanMetaData.AdminRoleID, clanEntity);
                    result?.Invoke(createCallback);
                    OnCreateClan?.Invoke(createCallback);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSCreateClanResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Find a clan by name. Full name is required.
        /// </summary>
        /// <param name="clanName"></param>
        /// <param name="result"></param>
        public void SearchClanByName(string clanName, Action<CBSGetClanEntityResult> result)
        {
            FabClan.SearchClan(clanName, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetClanEntityResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetClanEntityResult>();
                    var clanEntity = functionResult.ClanEntity;

                    result?.Invoke(new CBSGetClanEntityResult
                    {
                        IsSuccess = true,
                        ClanEntity = clanEntity
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetClanEntityResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get shot information about clan using constraints.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="constraints"></param>
        /// <param name="result"></param>
        public void GetClanEntity(string clanID, CBSClanConstraints constraints, Action<CBSGetClanEntityResult> result)
        {
            FabClan.GetClanEntity(clanID, constraints, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetClanEntityResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetClanEntityResult>();
                    var clanEntity = functionResult.ClanEntity;

                    result?.Invoke(new CBSGetClanEntityResult
                    {
                        IsSuccess = true,
                        ClanEntity = clanEntity
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetClanEntityResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get profile clan information.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="constraints"></param>
        /// <param name="result"></param>
        public void GetClanOfProfile(string profileID, CBSClanConstraints constraints, Action<CBSGetClanEntityResult> result)
        {
            FabClan.GetClanOfProfile(profileID, constraints, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetClanEntityResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetClanEntityResult>();
                    var clanEntity = functionResult.ClanEntity;

                    result?.Invoke(new CBSGetClanEntityResult
                    {
                        IsSuccess = true,
                        ClanEntity = clanEntity
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetClanEntityResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Send an invitation to a profile to join a clan.
        /// </summary>
        /// <param name="profileIDToInvite"></param>
        /// <param name="result"></param>
        public void InviteToClan(string profileIDToInvite, Action<CBSInviteToClanResult> result)
        {
            var profileID = Profile.ProfileID;
            FabClan.InviteToClan(profileID, profileIDToInvite, onInvite =>
            {
                var cbsError = onInvite.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSInviteToClanResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onInvite.GetResult<FunctionInviteToClanResult>();
                    var invitedProfileID = functionResult.ProfileID;
                    var roleID = functionResult.RoleId;
                    var expires = functionResult.Expires;

                    result?.Invoke(new CBSInviteToClanResult
                    {
                        IsSuccess = true,
                        ProfileID = invitedProfileID,
                        Expires = expires,
                        RoleId = roleID
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSInviteToClanResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get a list of all invitations of the current profile to join the clan.
        /// </summary>
        /// <param name="result"></param>
        public void GetProfileInvitations(CBSClanConstraints constraints, Action<CBSGetProfileInvationsResult> result)
        {
            var profileID = Profile.ProfileID;
            FabClan.GetProfileInvations(profileID, constraints, onInvite =>
            {
                var cbsError = onInvite.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetProfileInvationsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onInvite.GetResult<FunctionGetInvationsResult>();
                    var invations = functionResult.Invitations;

                    result?.Invoke(new CBSGetProfileInvationsResult
                    {
                        IsSuccess = true,
                        Invites = invations
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetProfileInvationsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Decline clan invitation to join.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void DeclineInvite(string clanID, Action<CBSDeclineInviteResult> result)
        {
            var profileID = Profile.ProfileID;
            FabClan.DeclineInvite(profileID, clanID, onDecline =>
            {
                var cbsError = onDecline.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSDeclineInviteResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var resultObject = new CBSDeclineInviteResult
                    {
                        IsSuccess = true,
                        ClanID = clanID
                    };
                    OnDeclineInvation?.Invoke(resultObject);
                    result?.Invoke(resultObject);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSDeclineInviteResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Accept the clan's invitation to join.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void AcceptInvite(string clanID, Action<CBSJoinToClanResult> result)
        {
            var profileID = Profile.ProfileID;
            FabClan.AcceptInvite(profileID, clanID, onInvite =>
            {
                var cbsError = onInvite.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSJoinToClanResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onInvite.GetResult<FunctionAcceptInvationResult>();
                    var clanEntity = functionResult.ClanEntity;
                    Get<CBSProfileModule>().ParseClanInfo(clanID, clanEntity);
                    Get<CBSClanModule>().ParseClanInfo(clanID, ClanMetaData.MemberRoleID, clanEntity);
                    var joinResult = new CBSJoinToClanResult
                    {
                        IsSuccess = true,
                        ClanID = clanID,
                        ClanEntity = clanEntity
                    };
                    OnProfileAcceptInvation?.Invoke(joinResult);
                    OnJoinClan?.Invoke(joinResult);
                    result?.Invoke(joinResult);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSJoinToClanResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Force join to clan if clan visibility is open
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void JoinToClan(string clanID, Action<CBSJoinToClanResult> result)
        {
            var profileID = Profile.ProfileID;
            FabClan.JoinToClan(profileID, clanID, onInvite =>
            {
                var cbsError = onInvite.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSJoinToClanResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onInvite.GetResult<FunctionAcceptInvationResult>();
                    var clanEntity = functionResult.ClanEntity;
                    Get<CBSProfileModule>().ParseClanInfo(clanID, clanEntity);
                    Get<CBSClanModule>().ParseClanInfo(clanID, ClanMetaData.MemberRoleID, clanEntity);
                    var joinResult = new CBSJoinToClanResult
                    {
                        IsSuccess = true,
                        ClanID = clanID,
                        ClanEntity = clanEntity
                    };
                    OnJoinClan?.Invoke(joinResult);
                    result?.Invoke(joinResult);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSJoinToClanResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Send an application to join the clan.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void SendJoinRequest(string clanID, Action<CBSBaseResult> result)
        {
            var profileID = Profile.ProfileID;

            FabClan.SendJoinRequest(profileID, clanID, onSent =>
            {
                var cbsError = onSent.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = true
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get a list of all profiles who want to join the clan.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void GetClanJoinRequestsList(CBSProfileConstraints constraints, Action<CBSGetClanJoinRequestListResult> result)
        {
            var clanID = Profile.ClanID;

            FabClan.GetJoinRequestList(clanID, constraints, onInvite =>
            {
                var cbsError = onInvite.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetClanJoinRequestListResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onInvite.GetResult<FunctionGetJoinRequestListResult>();
                    var requestList = functionResult.RequestList;

                    result?.Invoke(new CBSGetClanJoinRequestListResult
                    {
                        IsSuccess = true,
                        RequestList = requestList
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetClanJoinRequestListResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Accept the profile request to join the clan.
        /// </summary>
        /// <param name="profileIDToAccept"></param>
        /// <param name="result"></param>
        public void AcceptProfileJoinRequest(string profileIDToAccept, Action<CBSAcceptDeclineClanRequestResult> result)
        {
            var clanID = Profile.ClanID;
            var profileID = Profile.ProfileID;

            FabClan.AcceptJoinRequest(profileID, profileIDToAccept, clanID, onAccept =>
            {
                var cbsError = onAccept.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSAcceptDeclineClanRequestResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onAccept.GetResult<FunctionAcceptJoinRequestResult>();
                    var profileEntity = functionResult.ProfileEntity;
                    var resultObject = new CBSAcceptDeclineClanRequestResult
                    {
                        IsSuccess = true,
                        ProfileEntity = profileEntity
                    };
                    OnProfileAccepted?.Invoke(resultObject);
                    result?.Invoke(resultObject);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSAcceptDeclineClanRequestResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Decline the profile request to join the clan
        /// </summary>
        /// <param name="entityID"></param>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void DeclineProfileJoinRequest(string profileIDToDecline, Action<CBSAcceptDeclineClanRequestResult> result)
        {
            var clanID = Profile.ClanID;
            var profileID = Profile.ProfileID;

            FabClan.DeclineJoinRequest(profileID, profileIDToDecline, clanID, onAccept =>
            {
                var cbsError = onAccept.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSAcceptDeclineClanRequestResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var resultObject = new CBSAcceptDeclineClanRequestResult
                    {
                        IsSuccess = true
                    };
                    OnProfileDeclined?.Invoke(resultObject);
                    result?.Invoke(resultObject);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSAcceptDeclineClanRequestResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Leave current clan if exist.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="clanName"></param>
        /// <param name="result"></param>
        public void LeaveClan(Action<CBSBaseResult> result)
        {
            var profileID = Profile.ProfileID;
            var clanID = Profile.ClanID;

            FabClan.LeaveClan(profileID, clanID, onLeave =>
            {
                var cbsError = onLeave.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    Get<CBSProfileModule>().ParseClanInfo(string.Empty, null);
                    Get<CBSClanModule>().ParseClanInfo(string.Empty, string.Empty, null);
                    OnLeaveClan?.Invoke();
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = true
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Update the description of the clan the user is currently a member of
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="description"></param>
        /// <param name="result"></param>
        public void UpdateClanDescription(string description, Action<CBSBaseResult> result)
        {
            var profileID = Profile.ProfileID;
            var clanID = Profile.ClanID;

            FabClan.UpdateDescription(profileID, clanID, description, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = true
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Update the avatar info of the clan the user is currently a member of
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="url"></param>
        /// <param name="result"></param>
        public void UpdateClanAvatar(ClanAvatarInfo avatarInfo, Action<CBSBaseResult> result)
        {
            var profileID = Profile.ProfileID;
            var clanID = Profile.ClanID;

            FabClan.UpdateAvatar(profileID, clanID, avatarInfo, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = true
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Update the join request type of the clan the user is currently a member of
        /// </summary>
        /// <param name="visibility"></param>
        /// <param name="result"></param>
        public void UpdateClanVisibility(ClanVisibility visibility, Action<CBSBaseResult> result)
        {
            var profileID = Profile.ProfileID;
            var clanID = Profile.ClanID;

            FabClan.UpdateVisibility(profileID, clanID, visibility, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = true
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Update the display name of the clan the user is currently a member of
        /// </summary>
        /// <param name="visibility"></param>
        /// <param name="result"></param>
        public void UpdateClanDisplayName(string displayName, Action<CBSBaseResult> result)
        {
            var profileID = Profile.ProfileID;
            var clanID = Profile.ClanID;

            FabClan.UpdateDisplayName(profileID, clanID, displayName, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = true
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Set clan clan custom data by update request.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="updateRequest"></param>
        /// <param name="result"></param>
        public void UpdateClanCustomData(string clanID, Dictionary<string, string> updateRequest, Action<CBSBaseResult> result)
        {
            var profileID = Profile.ProfileID;

            FabClan.UpdateCustomData(profileID, clanID, updateRequest, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = true
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get clan all custom data
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void GetClanCustomData(string clanID, Action<CBSGetClanCustomDataResult> result)
        {
            var profileID = Profile.ProfileID;

            FabClan.GetCustomData(profileID, clanID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetClanCustomDataResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionCustomDataResult>();
                    var dataClanID = functionResult.ClanID;
                    var customData = functionResult.CustomData;

                    result?.Invoke(new CBSGetClanCustomDataResult
                    {
                        IsSuccess = true,
                        ClanID = clanID,
                        CustomData = customData
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetClanCustomDataResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get information about requests/invations count for profile
        /// </summary>
        /// <param name="result"></param>
        public void GetClanBadge(Action<CBSGetClanBadgeResult> result)
        {
            var profileID = Profile.ProfileID;

            FabClan.GetBadge(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetClanBadgeResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetClanBadgeResult>();
                    var requestsCount = functionResult.RequestsCount;
                    var invationsCount = functionResult.InvationsCount;

                    result?.Invoke(new CBSGetClanBadgeResult
                    {
                        IsSuccess = true,
                        RequestsCount = requestsCount,
                        InvationsCount = invationsCount
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetClanBadgeResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get a list of all clan members.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void GetClanMemberships(string clanID, CBSProfileConstraints constraints, Action<CBSGetClanMembersResult> result)
        {
            var profileID = Profile.ProfileID;

            FabClan.GetClanMembers(clanID, constraints, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetClanMembersResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetClanMembersResult>();
                    var clanEntityID = functionResult.ClanID;
                    var availbaleRoles = functionResult.AvailableRoles;
                    var members = functionResult.Members;

                    result?.Invoke(new CBSGetClanMembersResult
                    {
                        IsSuccess = true,
                        ClanID = clanEntityID,
                        AvailableRoles = availbaleRoles,
                        Members = members
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetClanMembersResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Remove a member from the clan.
        /// </summary>
        /// <param name="entityID"></param>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void KickClanMember(string profileIDToKick, Action<CBSBaseResult> result)
        {
            var profileID = Profile.ProfileID;
            var clanID = Profile.ClanID;

            FabClan.KickMember(profileID, profileIDToKick, clanID, onKick =>
            {
                var cbsError = onKick.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = true
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Change clan member role is profile has permission for this action
        /// </summary>
        /// <param name="memberProfileID"></param>
        /// <param name="newRoleID"></param>
        /// <param name="result"></param>
        public void ChangeMemberRole(string memberProfileID, string newRoleID, Action<CBSChangeRoleResult> result)
        {
            var profileID = Profile.ProfileID;
            var clanID = Profile.ClanID;

            FabClan.ChangeMemberRole(profileID, memberProfileID, clanID, newRoleID, onChange =>
            {
                var cbsError = onChange.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSChangeRoleResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onChange.GetResult<FunctionChangeClanRoleResult>();
                    var memberID = functionResult.ProfileID;
                    var newRole = functionResult.NewRole;

                    result?.Invoke(new CBSChangeRoleResult
                    {
                        IsSuccess = true,
                        ProfileID = memberID,
                        NewRole = newRole
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSChangeRoleResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Disband current clan.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="clanName"></param>
        /// <param name="result"></param>
        public void DisbandClan(Action<CBSBaseResult> result)
        {
            var profileID = Profile.ProfileID;
            var clanID = Profile.ClanID;

            FabClan.DisbandClan(profileID, clanID, onDisband =>
            {
                var cbsError = onDisband.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    Get<CBSProfileModule>().ParseClanInfo(string.Empty, null);
                    Get<CBSClanModule>().ParseClanInfo(string.Empty, string.Empty, null);
                    OnLeaveClan?.Invoke();
                    OnDisbandClan?.Invoke();

                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = true
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Adds N points of experience to the current state. In the response, you can get information whether the clan has reached a new level and also information about the reward about the new level.
        /// </summary>
        /// <param name="expToAdd"></param>
        /// <param name="result"></param>
        public void AddExpirienceToClan(string clanID, int expToAdd, Action<CBSLevelDataResult> result = null)
        {
            var profileID = Profile.ProfileID;

            FabClan.AddExpirience(profileID, clanID, expToAdd, onSuccess =>
            {
                var cbsError = onSuccess.GetCBSError();
                if (cbsError == null)
                {
                    var functionResult = onSuccess.GetResult<FunctionAddExpirienceResult>();
                    var isNewLevel = functionResult.NewLevelReached;
                    var rewardResult = functionResult.NewLevelReward;

                    var callbackData = new CBSLevelDataResult
                    {
                        IsSuccess = true,
                        LevelInfo = functionResult,
                        IsNewLevel = isNewLevel,
                        NewLevelReward = rewardResult
                    };

                    result?.Invoke(callbackData);
                }
                else
                {
                    var failedResult = new CBSLevelDataResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };

                    result?.Invoke(failedResult);
                }
            }, onError =>
            {

                var failedResult = new CBSLevelDataResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };

                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Get information about current experience/level of clan
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void GetClanLevelDetail(string clanID, Action<CBSLevelDataResult> result)
        {
            var profileID = Profile.ProfileID;

            FabClan.GetLevelInfo(profileID, clanID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError == null)
                {
                    var functionResult = onGet.GetResult<LevelInfo>();

                    var callbackData = new CBSLevelDataResult
                    {
                        IsSuccess = true,
                        LevelInfo = functionResult
                    };

                    result?.Invoke(callbackData);
                }
                else
                {
                    var failedResult = new CBSLevelDataResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(failedResult);
                }
            }, onError =>
            {

                var failedResult = new CBSLevelDataResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };

                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Get inventory items list of clan
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void GetClanInventory(string clanID, Action<CBSGetInventoryResult> result)
        {
            var authProfileID = Profile.ProfileID;

            FabClan.GetInventory(authProfileID, clanID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetInventoryResult()
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetInventoryResult>();
                    var instances = functionResult.Instances;

                    var cbsInstances = instances.Select(x => x.ToCBSInventoryItem()).ToList();
                    var resultObject = new CBSGetInventoryResult(cbsInstances, clanID);
                    resultObject.IsSuccess = true;
                    result?.Invoke(resultObject);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetInventoryResult()
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Add item to clan by id. The item automatically goes into inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="result"></param>
        public void GrantItemToClan(string clanID, string itemID, Action<CBSGrantItemsResult> result)
        {
            var profileID = Profile.ProfileID;
            var itemsIDs = new string[] { itemID };
            InternalGrantItemsToClan(profileID, clanID, itemsIDs, result);
        }

        /// <summary>
        /// Add items to clan by id. The items automatically goes into inventory.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="itemsID"></param>
        /// <param name="result"></param>
        public void GrantItemsToClan(string clanID, string[] itemsID, Action<CBSGrantItemsResult> result)
        {
            var profileID = Profile.ProfileID;
            InternalGrantItemsToClan(profileID, clanID, itemsID, result);
        }

        /// <summary>
        /// Get information about clan currencies
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        public void GetClanCurrencies(string clanID, Action<CBSGetCurrenciesResult> result)
        {
            var authProfileID = Profile.ProfileID;
            FabClan.GetCurrency(authProfileID, clanID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSGetCurrenciesResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionCurrenciesResult>();
                    var currencies = functionResult.Currencies;

                    var callback = new CBSGetCurrenciesResult
                    {
                        IsSuccess = true,
                        TargetID = clanID,
                        Currencies = currencies
                    };
                    result?.Invoke(callback);
                }
            },
            onError =>
            {
                var callback = new CBSGetCurrenciesResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Add currency value to current clan balance
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="code"></param>
        /// <param name="amount"></param>
        /// <param name="result"></param>
        public void AddCurrencyToClan(string clanID, string code, int amount, Action<CBSUpdateCurrencyResult> result = null)
        {
            var authProfileID = Profile.ProfileID;
            FabClan.AddCurrency(authProfileID, clanID, code, amount, onChange =>
            {
                var cbsError = onChange.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSUpdateCurrencyResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onChange.GetResult<FunctionChangeCurrencyResult>();
                    result?.Invoke(new CBSUpdateCurrencyResult
                    {
                        IsSuccess = true,
                        TargetID = functionResult.TargetID,
                        UpdatedCurrency = functionResult.UpdatedCurrency,
                        BalanceChange = functionResult.BalanceChange,
                    });

                }
            },
            onError =>
            {
                var callback = new CBSUpdateCurrencyResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Subtract currency value from current clan balance.
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="code"></param>
        /// <param name="amount"></param>
        /// <param name="result"></param>
        public void SubtractCurrencyFromClan(string clanID, string code, int amount, Action<CBSUpdateCurrencyResult> result = null)
        {
            var authProfileID = Profile.ProfileID;
            FabClan.SubtractCurrency(authProfileID, clanID, code, amount, onChange =>
            {
                var cbsError = onChange.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSUpdateCurrencyResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onChange.GetResult<FunctionChangeCurrencyResult>();
                    result?.Invoke(new CBSUpdateCurrencyResult
                    {
                        IsSuccess = true,
                        TargetID = functionResult.TargetID,
                        UpdatedCurrency = functionResult.UpdatedCurrency,
                        BalanceChange = functionResult.BalanceChange,
                    });

                }
            },
            onError =>
            {
                var callback = new CBSUpdateCurrencyResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Move item from profile inventory to clan inventory
        /// </summary>
        /// <param name="itemInstanceID"></param>
        /// <param name="result"></param>
        public void TransferItemFromProfileToClan(string itemInstanceID, Action<CBSClanTransferItemResult> result)
        {
            var authProfileID = Profile.ProfileID;
            var clanID = Profile.ClanID;
            var authContext = Profile.AuthenticationContex;

            FabClan.TransferItemFromProfileToClan(authProfileID, clanID, authContext, itemInstanceID, onChange =>
            {
                var cbsError = onChange.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSClanTransferItemResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onChange.GetResult<CBSClanTransferItemResult>();
                    result?.Invoke(new CBSClanTransferItemResult
                    {
                        IsSuccess = true,
                        ProfileID = functionResult.ProfileID,
                        ClanID = functionResult.ClanID,
                        ItemInstanceID = functionResult.ItemInstanceID,
                        TransferID = functionResult.TransferID
                    });

                }
            },
            onError =>
            {
                var callback = new CBSClanTransferItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Move item from clan inventory to profile inventory
        /// </summary>
        /// <param name="itemInstanceID"></param>
        /// <param name="result"></param>
        public void TransferItemFromClanToProfile(string itemInstanceID, Action<CBSClanTransferItemResult> result)
        {
            var authProfileID = Profile.ProfileID;
            var clanID = Profile.ClanID;
            var authContext = Profile.AuthenticationContex;

            FabClan.TransferItemFromClanToProfile(authProfileID, clanID, authContext, itemInstanceID, onChange =>
            {
                var cbsError = onChange.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSClanTransferItemResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onChange.GetResult<CBSClanTransferItemResult>();
                    result?.Invoke(new CBSClanTransferItemResult
                    {
                        IsSuccess = true,
                        ProfileID = functionResult.ProfileID,
                        ClanID = functionResult.ClanID,
                        ItemInstanceID = functionResult.ItemInstanceID,
                        TransferID = functionResult.TransferID
                    });

                }
            },
            onError =>
            {
                var callback = new CBSClanTransferItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Get tasks available for current profile clan
        /// </summary>
        /// <param name="result"></param>
        public void GetTasksForClan(Action<CBSGetTasksForClanResult> result)
        {
            var authProfileID = Profile.ProfileID;
            var clanID = Profile.ClanID;

            FabClan.GetTasksForClan(authProfileID, clanID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSGetTasksForClanResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionClanTasksResult>();
                    result?.Invoke(new CBSGetTasksForClanResult
                    {
                        IsSuccess = true,
                        Tasks = functionResult.Tasks,
                        ResetDate = functionResult.NextResetDate
                    });

                }
            },
            onError =>
            {
                var callback = new CBSGetTasksForClanResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Adds a point to an task. For Tasks "OneShot" completes it immediately, for Tasks "Steps" - adds one step
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        public void AddTaskPoint(string taskID, Action<CBSModifyClanTaskPointsResult> result)
        {
            var clanID = Profile.ClanID;
            InternalModifyPoints(clanID, taskID, 1, ModifyMethod.ADD, result);
        }

        /// <summary>
        /// Adds the points you specified to the task. More suitable for Steps task.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        public void AddTaskPoints(string taskID, int points, Action<CBSModifyClanTaskPointsResult> result)
        {
            var clanID = Profile.ClanID;
            InternalModifyPoints(clanID, taskID, points, ModifyMethod.ADD, result);
        }

        /// <summary>
        /// Reset tasks for current profile clan
        /// </summary>
        /// <param name="result"></param>
        public void ResetTasksForClan(Action<CBSGetTasksForClanResult> result)
        {
            var authProfileID = Profile.ProfileID;
            var clanID = Profile.ClanID;

            FabClan.ResetTasksForClan(authProfileID, clanID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    var resultObject = new CBSGetTasksForClanResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(resultObject);
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionClanTasksResult>();
                    result?.Invoke(new CBSGetTasksForClanResult
                    {
                        IsSuccess = true,
                        Tasks = functionResult.Tasks,
                        ResetDate = functionResult.NextResetDate
                    });

                }
            },
            onError =>
            {
                var callback = new CBSGetTasksForClanResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(callback);
            });
        }

        // internal
        private void InternalGrantItemsToClan(string profileID, string clanID, string[] itemsIDs, Action<CBSGrantItemsResult> result)
        {
            var authProfileID = Profile.ProfileID;
            var containPack = Get<CBSItemsModule>().IsItemsFromPack(itemsIDs);
            FabClan.GrantItems(profileID, clanID, itemsIDs, containPack, onGrant =>
            {
                var cbsError = onGrant.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGrantItemsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGrant.GetResult<FunctionGrantItemsResult>();
                    var grantedInstances = functionResult.GrantedInstances;
                    var grantedCurrencies = functionResult.GrantedCurrencies;
                    var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();

                    var resultObject = new CBSGrantItemsResult
                    {
                        IsSuccess = true,
                        TargetID = profileID,
                        GrantedInstances = inventoryItems,
                        GrantedCurrencies = grantedCurrencies
                    };
                    result?.Invoke(resultObject);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGrantItemsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        private void InternalModifyPoints(string clanID, string taskID, int points, ModifyMethod modify, Action<CBSModifyClanTaskPointsResult> result)
        {
            var profileID = Profile.ProfileID;

            FabClan.ModifyTasksPoint(profileID, clanID, taskID, points, modify, onAdd =>
            {
                var cbsError = onAdd.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyClanTaskPointsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onAdd.GetResult<FunctionModifyTaskResult<CBSClanTask>>();
                    var task = functionResult.Task;
                    var reward = functionResult.RewardResult;
                    var complete = functionResult.CompleteTask;
                    var completeTier = functionResult.CompleteTier;

                    result?.Invoke(new CBSModifyClanTaskPointsResult
                    {
                        IsSuccess = true,
                        Task = task,
                        ReceivedReward = reward,
                        CompleteTask = complete,
                        CompleteTier = completeTier
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyClanTaskPointsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        internal void ParseClanInfo(string clanID, string roleID, ClanEntity clanEntity)
        {
            CurrentRoleID = roleID;
            CurrentClanID = clanID;
            CurrentClanEntity = clanEntity;
            ExistInClan = !string.IsNullOrEmpty(clanID);
        }
    }
}
