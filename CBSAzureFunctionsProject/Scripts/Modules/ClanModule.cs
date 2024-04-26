using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using PlayFab.AdminModels;
using PlayFab.GroupsModels;
using PlayFab;
using PlayFab.DataModels;

namespace CBS
{
    public class ClanModule : BaseAzureModule
    {
        private static readonly string AuthDataKey = "Auth";
        private static readonly string GroupIDKey = "GroupID";
        private static readonly string GroupProfileKey = "GroupProfile";
        private static readonly string AvatarInfoKey = "Avatar";
        private static readonly string DescriptionKey = "Description";
        private static readonly string VisibilityKey = "Visibility";

        private static readonly string UnknownRoleName = "Unknown";

        [FunctionName(AzureFunctions.CreateClanMethod)]
        public static async Task<dynamic> CreateClanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionCreateClanRequest>();

            var createResult = await CreateClanAsync(request);
            if (createResult.Error != null)
            {
                return ErrorHandler.ThrowError(createResult.Error).AsFunctionResult();
            }

            return createResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.FindClanMethod)]
        public static async Task<dynamic> FindClanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var clanName = request.ID;

            var searchResult = await SearchClanByNameAsync(clanName);
            if (searchResult.Error != null)
            {
                return ErrorHandler.ThrowError(searchResult.Error).AsFunctionResult();
            }

            return searchResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetClanEntityMethod)]
        public static async Task<dynamic> GetClanEntityTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<GetClanEntityRequest>();
            var clanID = request.ClanID;
            var constraints = request.Constraints;

            var getResult = await GetClanEntityAsync(clanID, constraints);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetClanOfProfileMethod)]
        public static async Task<dynamic> GetClanOfProfileTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<GetClanEntityRequest>();
            var profileID = request.ProfileID;
            var constraints = request.Constraints;

            var getResult = await GetClanOfProfileAsync(profileID, constraints);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.InviteToClanMethod)]
        public static async Task<dynamic> InviteToClanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var profileIDToRequest = request.ID;

            var inviteResult = await InviteToClanAsync(profileID, profileIDToRequest);
            if (inviteResult.Error != null)
            {
                return ErrorHandler.ThrowError(inviteResult.Error).AsFunctionResult();
            }

            return inviteResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileInvationsMethod)]
        public static async Task<dynamic> GetProfileInvationsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetInvationsRequest>();
            var profileID = request.ProfileID;
            var constraints = request.Constraints;

            var getResult = await GetProfileInvationsAsync(profileID, constraints);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AcceptClanInvationMethod)]
        public static async Task<dynamic> AcceptClanInvationTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;

            var acceptResult = await AcceptInvationAsync(profileID, clanID);
            if (acceptResult.Error != null)
            {
                return ErrorHandler.ThrowError(acceptResult.Error).AsFunctionResult();
            }

            return acceptResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.DeclineClanInvationMethod)]
        public static async Task<dynamic> DeclineClanInvationTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;

            var declineResult = await DeclineInvationAsync(profileID, clanID);
            if (declineResult.Error != null)
            {
                return ErrorHandler.ThrowError(declineResult.Error).AsFunctionResult();
            }

            return declineResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.JoinToClanMethod)]
        public static async Task<dynamic> JoinToClanMethodTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;

            var joinResult = await JoinClanAsync(profileID, clanID);
            if (joinResult.Error != null)
            {
                return ErrorHandler.ThrowError(joinResult.Error).AsFunctionResult();
            }

            return joinResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SendClanJoinRequestMethod)]
        public static async Task<dynamic> SendClanJoinRequestTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;

            var sendResult = await SendJoinRequestAsync(profileID, clanID);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError(sendResult.Error).AsFunctionResult();
            }

            return sendResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetClanJoinRequestListMethod)]
        public static async Task<dynamic> GetClanJoinRequestListTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetClanProfilesRequest>();
            var clanID = request.ClanID;
            var constraints = request.Constraints;

            var getResult = await GetJoinRequestListAsync(clanID, constraints);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AcceptClanJoinRequestMethod)]
        public static async Task<dynamic> AcceptClanJoinRequestTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionAcceptJoinRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var profileIDToJoin = request.ProfileIDToJoin;

            var acceptResult = await AcceptJoinRequestAsync(profileID, profileIDToJoin, clanID);
            if (acceptResult.Error != null)
            {
                return ErrorHandler.ThrowError(acceptResult.Error).AsFunctionResult();
            }

            return acceptResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.DeclineClanJoinRequestMethod)]
        public static async Task<dynamic> DeclineClanJoinRequestTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionAcceptJoinRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var profileIDToDecline = request.ProfileIDToJoin;

            var declineResult = await DeclineJoinRequestAsync(profileID, profileIDToDecline, clanID);
            if (declineResult.Error != null)
            {
                return ErrorHandler.ThrowError(declineResult.Error).AsFunctionResult();
            }

            return declineResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.LeaveClanMethod)]
        public static async Task<dynamic> LeaveClanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;

            var leaveResult = await LeaveClanAsync(profileID, clanID);
            if (leaveResult.Error != null)
            {
                return ErrorHandler.ThrowError(leaveResult.Error).AsFunctionResult();
            }

            return leaveResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetClanFullInformationMethod)]
        public static async Task<dynamic> GetClanFullInformationTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanRequest>();
            var clanID = request.ClanID;

            var getResult = await GetClanFullInfoAsync(clanID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateClanDisplayNameMethod)]
        public static async Task<dynamic> UpdateClanDisplayNameTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateClanMetaDataRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var displayName = request.DisplayName;

            var updateResult = await SetClanDisplayNameAsync(clanID, displayName, profileID);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateClanDescriptionMethod)]
        public static async Task<dynamic> UpdateClanDescriptionTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateClanMetaDataRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var description = request.Description;

            var updateResult = await SetClanDescriptionAsync(clanID, description, profileID);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateClanVisibilityMethod)]
        public static async Task<dynamic> UpdateClanVisibilityTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateClanMetaDataRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var visibility = request.Visibility;

            var updateResult = await SetClanVisibilityAsync(clanID, visibility, profileID);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateClanAvatarMethod)]
        public static async Task<dynamic> UpdateClanAvatarTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateClanMetaDataRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var avatarInfo = request.AvatarInfo;

            var updateResult = await SetClanAvatarAsync(clanID, avatarInfo, profileID);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateClanCustomDataMethod)]
        public static async Task<dynamic> UpdateClanCustomDataTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateCustomDataRequest>();
            var clanID = request.ClanID;
            var updateRequest = request.UpdateRequest;

            var updateResult = await SetClanCustomDataAsync(clanID, updateRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetClanCustomDataMethod)]
        public static async Task<dynamic> GetClanCustomDataTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanRequest>();
            var clanID = request.ClanID;

            var getResult = await GetClanCustomDataAsync(clanID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetClanBadgeMethod)]
        public static async Task<dynamic> GetClanBadgeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();
            var profileID = request.ProfileID;

            var getResult = await GetClanBadgeAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetClanMembersMethod)]
        public static async Task<dynamic> GetClanMembersTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetClanProfilesRequest>();
            var clanID = request.ClanID;
            var constraints = request.Constraints;

            var getResult = await GetClanMembersAsync(clanID, constraints);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.KickClanMemberMethod)]
        public static async Task<dynamic> KickClanMemberTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionKickClanMemberRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var kickProfileID = request.ProfileIDToKick;

            var getResult = await KickMemberAsync(profileID, kickProfileID, clanID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.ChangeClanMemberRoleMethod)]
        public static async Task<dynamic> ChangeClanMemberRoleTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionChangeRoleRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;
            var profileIDToChange = request.ProfileIDToChange;
            var newRoleID = request.NewRoleID;

            var changeResult = await ChangeMemberRoleAsync(profileID, profileIDToChange, clanID, newRoleID);
            if (changeResult.Error != null)
            {
                return ErrorHandler.ThrowError(changeResult.Error).AsFunctionResult();
            }

            return changeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.DisbandClanMethod)]
        public static async Task<dynamic> DisbandClanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ClanID;

            var disbandResult = await DisbandClanAsync(profileID, clanID);
            if (disbandResult.Error != null)
            {
                return ErrorHandler.ThrowError(disbandResult.Error).AsFunctionResult();
            }

            return disbandResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionCreateClanResult>> CreateClanAsync(FunctionCreateClanRequest request)
        {
            // check inputs
            if (!request.IsValid())
            {
                return ErrorHandler.InvalidInput<FunctionCreateClanResult>();
            }

            var creatorID = request.ProfileID;
            var creatorEntityID = request.EntityID;
            var clanDisplayName = request.DisplayName;
            var clanDescription = request.Description;
            var clanCustomData = request.CustomData;
            var clanVisibility = request.Visibility;
            var clanAvatarInfo = request.AvatarInfo;

            // get clan meta data
            var clanMetaResult = await GetClanMetaDataAsync();
            if (clanMetaResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(clanMetaResult.Error);
            }
            var clanMeta = clanMetaResult.Result;
            var customRoles = clanMeta.GetCustomRoleList();

            // check if displayname available
            var profanityCheck = clanMeta.DisplayNameProfanityCheck;
            var availableCheckResult = await CheckClanDisplayNameAvailabilityAsync(clanDisplayName, profanityCheck);
            if (availableCheckResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(availableCheckResult.Error);
            }
            var nicknameAvailable = availableCheckResult.Result.Value;
            if (!nicknameAvailable)
            {
                return ErrorHandler.DisplayNameNotAvailable<FunctionCreateClanResult>();
            }
            
            // register clan profile
            var authID = System.Guid.NewGuid().ToString();
            PlayFabSettings.staticSettings.TitleId = TitleId;
            PlayFabSettings.staticSettings.DeveloperSecretKey = SercetKey;
            var authRequest = new PlayFab.ClientModels.LoginWithCustomIDRequest
            {
                CustomId = authID,
                CreateAccount = true
            };
            var registerResult = await FabClientAPI.LoginWithCustomIDAsync(authRequest);
            if (registerResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(registerResult.Error);
            }
            var clanProfile = registerResult.Result;
            var clanID = clanProfile.PlayFabId;

            // update clan display name
            var setNameResult = await SetClanDisplayNameAsync(clanID, clanDisplayName);
            if (setNameResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(setNameResult.Error);
            }

            // create group
            var entityKey = new PlayFab.GroupsModels.EntityKey {
                Id = creatorEntityID,
                Type = ProfileEntityKey
            };
            var createRequest = new CreateGroupRequest {
                GroupName = authID,
                Entity = entityKey
            };
            var groupApi = await GetFabGroupAPIAsync();
            var createGroupResult = await groupApi.CreateGroupAsync(createRequest);
            if (createGroupResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(createGroupResult.Error);
            }
            var clanGroup = createGroupResult.Result;
            var groupEntity = clanGroup.Group;
            var groupID = clanGroup.Group.Id;

            // asign profile to group
            var fabDataAPI = await GetFabDataAPIAsync();
            var objectToUpdate = new List<SetObject>();
            objectToUpdate.Add(new SetObject { 
                ObjectName = GroupProfileKey,
                DataObject = clanID
            });
            var asignRequest = new PlayFab.DataModels.SetObjectsRequest
            {
                Entity = groupEntity.ToDataEntity(),
                Objects = objectToUpdate
            };
            var asignResult = await fabDataAPI.SetObjectsAsync(asignRequest);
            if (asignResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(asignResult.Error);
            }

            // apply new roles
            var hasNewRoles = customRoles.Count > 0;
            if (hasNewRoles)
            {
                foreach (var role in customRoles)
                {
                    var roleRequest = new CreateGroupRoleRequest
                    {
                        RoleId = role.RoleID,
                        RoleName = role.DisplayName,
                        Group = groupEntity
                    };
                    await groupApi.CreateRoleAsync(roleRequest);
                }
            }

            // set clan internal data
            var internalData = new Dictionary<string, string>();
            internalData[AuthDataKey] = authID;
            internalData[GroupIDKey] = groupID;
            
            var updateDataRequest = new PlayFab.ServerModels.UpdateUserInternalDataRequest {
                PlayFabId = clanID,
                Data = internalData
            };      
            var updateResult = await FabServerAPI.UpdateUserInternalDataAsync(updateDataRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(updateResult.Error);
            }

            // save avatar
            if (clanAvatarInfo != null)
            {
                var saveAvatarResult = await SetClanAvatarAsync(clanID, clanAvatarInfo);
                if (saveAvatarResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionCreateClanResult>(saveAvatarResult.Error);
                }
            }

            // save description
            if (!string.IsNullOrEmpty(clanDescription))
            {
                var saveDescriptionResult = await SetClanDescriptionAsync(clanID, clanDescription);
                if (saveDescriptionResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionCreateClanResult>(saveDescriptionResult.Error);
                }
            }

            // save visibility
            var saveVisibilityResult = await SetClanVisibilityAsync(clanID, clanVisibility);
            if (saveVisibilityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(saveVisibilityResult.Error);
            }

            // save custom data
            if (clanCustomData != null && clanCustomData.Count > 0)
            {
                var saveCustomDataResult = await SetClanCustomDataAsync(clanID, clanCustomData);
                if (saveCustomDataResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionCreateClanResult>(saveCustomDataResult.Error);
                }
            }

            // leave clan if exist
            var leaveResult = await LeaveClanIfExistAsync(creatorID);
            if (leaveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(leaveResult.Error);
            }

            // process join to clan (creator)
            var joiningResult = await ProcessJoiningToClanAsync(creatorID, clanID);
            if (joiningResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(joiningResult.Error);
            }

            // get clan entity
            var getClanEntityResult = await GetClanEntityAsync(clanID, CBSClanConstraints.Full());
            if (getClanEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCreateClanResult>(getClanEntityResult.Error);
            }
            var clanEntity = getClanEntityResult.Result.ClanEntity;

            return new ExecuteResult<FunctionCreateClanResult>
            {
                Result = new FunctionCreateClanResult
                {
                    ClanID = clanID,
                    GroupID = groupID,
                    ClanEntity = clanEntity
                }
            };
        }

        public static async Task<ExecuteResult<ClanMetaData>> GetClanMetaDataAsync()
        {
            var dataResult = await GetInternalTitleDataAsObjectAsync<ClanMetaData>(TitleKeys.ClanMetaDataKey);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<ClanMetaData>(dataResult.Error);
            }
            return new ExecuteResult<ClanMetaData>
            {
                Result = dataResult.Result ?? new ClanMetaData()
            };
        }

        public static async Task<ExecuteResult<FunctionSetClanDisplayNameResult>> SetClanDisplayNameAsync(string clanID, string displayName, string profileID = null)
        {
            if (!string.IsNullOrEmpty(profileID))
            {
                var hasPermissionResult = await HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.CHANGE_META_DATA);
                if (hasPermissionResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionSetClanDisplayNameResult>(hasPermissionResult.Error);
                }
                var hasPermission = hasPermissionResult.Result.Value;
                if (!hasPermission)
                {
                    return ErrorHandler.NotEnoughRights<FunctionSetClanDisplayNameResult>();
                }
            }

            // get clan meta data
            var clanMetaResult = await GetClanMetaDataAsync();
            if (clanMetaResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSetClanDisplayNameResult>(clanMetaResult.Error);
            }
            var clanMeta = clanMetaResult.Result;

            // check if displayname available
            var profanityCheck = clanMeta.DisplayNameProfanityCheck;
            var availableCheckResult = await CheckClanDisplayNameAvailabilityAsync(displayName, profanityCheck);
            if (availableCheckResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSetClanDisplayNameResult>(availableCheckResult.Error);
            }

            var request = new UpdateUserTitleDisplayNameRequest
            {
                PlayFabId = clanID,
                DisplayName = displayName
            };
            var adminAPI = await GetFabAdminAPIAsync();
            var result = await adminAPI.UpdateUserTitleDisplayNameAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSetClanDisplayNameResult>(result.Error);
            }

            var tableUpdateResult = await TableClanAssistant.UpdateClanDisplayNameAsync(clanID, displayName);
            if (tableUpdateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSetClanDisplayNameResult>(tableUpdateResult.Error);
            }

            return new ExecuteResult<FunctionSetClanDisplayNameResult>
            {
                Result = new FunctionSetClanDisplayNameResult
                {
                    DisplayName = displayName
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetClanDisplayNameResult>> GetClanDisplayNameAsync(string clanID)
        {
            var accountRequest = new PlayFab.ServerModels.GetUserAccountInfoRequest
            {
                PlayFabId = clanID
            };
            var accountInfoResult = await FabServerAPI.GetUserAccountInfoAsync(accountRequest);
            if (accountInfoResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanDisplayNameResult>(accountInfoResult.Error);
            }
            var accountInfo = accountInfoResult.Result.UserInfo;
            var displayName = accountInfo.TitleInfo.DisplayName;

            return new ExecuteResult<FunctionGetClanDisplayNameResult>
            {
                Result = new FunctionGetClanDisplayNameResult
                {
                    DisplayName = displayName
                }
            };
        }

        public static async Task<ExecuteResult<FunctionBoolResult>> CheckClanDisplayNameAvailabilityAsync(string displayName, bool profanityCheck = false)
        {
            var request = new PlayFab.AdminModels.LookupUserAccountInfoRequest
            {
                TitleDisplayName = displayName
            };
            var adminAPI = await GetFabAdminAPIAsync();
            var lookupResult = await adminAPI.GetUserAccountInfoAsync(request);
            if (lookupResult.Error != null)
            {
                if (lookupResult.Error.Error == PlayFab.PlayFabErrorCode.AccountNotFound)
                {
                    return new ExecuteResult<FunctionBoolResult>
                    {
                        Result = new FunctionBoolResult
                        {
                            Value = true
                        }
                    };
                }
                return ErrorHandler.ThrowError<FunctionBoolResult>(lookupResult.Error);
            }
            var resultInfo = lookupResult.Result.UserInfo;
            var nickNameAvailable = resultInfo == null;

            if (profanityCheck)
            {
                var hasCensoredWord = Censor.HasCensoredWord(displayName);
                if (hasCensoredWord)
                    nickNameAvailable = false;
            }

            return new ExecuteResult<FunctionBoolResult>
            {
                Result = new FunctionBoolResult
                {
                    Value = nickNameAvailable
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> LeaveClanIfExistAsync(string profileID)
        {
            var getProfileClanResult  = await ProfileModule.GetProfileClanIDAsync(profileID);
            if (getProfileClanResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(getProfileClanResult.Error);
            }
            var clanID = getProfileClanResult.Result;
            if (string.IsNullOrEmpty(clanID))
            {
                return new ExecuteResult<FunctionEmptyResult>
                {
                    Result = new FunctionEmptyResult()
                };
            }
            else
            {
                // process leave
                var leaveResult = await LeaveClanAsync(profileID, clanID);
                if (leaveResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(leaveResult.Error);
                }
                return new ExecuteResult<FunctionEmptyResult>
                {
                    Result = new FunctionEmptyResult()
                };
            }
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ProcessJoiningToClanAsync(string profileID, string clanID)
        {
            var saveToProfileResult = await ProfileModule.SetProfileClanIDAsync(profileID, clanID);
            if (saveToProfileResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveToProfileResult.Error);
            }

            var recalculateResult = await RecalculateClanMembersCountAsync(clanID);
            if (recalculateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(recalculateResult.Error);
            }

            var metaDataResult = await GetClanMetaDataAsync();
            if (metaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(metaDataResult.Error);
            }
            var metaData = metaDataResult.Result;
            var sendJoinMessage = metaData.SendJoinMessage;
            if (sendJoinMessage)
            {
                var getProfileEntity = await TableProfileAssistant.GetProfileDetailAsync(profileID, new CBSProfileConstraints());
                if (getProfileEntity.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(getProfileEntity.Error);
                }
                var profileName = getProfileEntity.Result.DisplayName;
                var chatMessage = string.Format(ClanMetaData.JoinMessageTemplate, profileName);
                var sendMessageResult = await ChatModule.SendSystemMessageToChatAsync(clanID, chatMessage);
                if (sendMessageResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(sendMessageResult.Error);
                }
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> ProcessLeaveClanAsync(string profileID, string clanID)
        {
            var saveToProfileResult = await ProfileModule.SetProfileClanIDAsync(profileID, string.Empty);
            if (saveToProfileResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(saveToProfileResult.Error);
            }

            var memberCountResult = await GetClanMemberCountAsync(clanID);
            if (memberCountResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(memberCountResult.Error);
            }
            var membersCount = memberCountResult.Result.Value;
            if (membersCount == 0)
            {
                var disbandResult = await DisbandClanAsync(profileID, clanID, true);
                if (disbandResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(memberCountResult.Error);
                }
                else
                {
                    return new ExecuteResult<FunctionEmptyResult>
                    {
                        Result = new FunctionEmptyResult()
                    };
                }
            }

            var recalculateResult = await RecalculateClanMembersCountAsync(clanID);
            if (recalculateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(recalculateResult.Error);
            }

            var metaDataResult = await GetClanMetaDataAsync();
            if (metaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(metaDataResult.Error);
            }
            var metaData = metaDataResult.Result;
            var sendLeftMessage = metaData.SendLeaveMessage;
            if (sendLeftMessage)
            {
                var getProfileEntity = await TableProfileAssistant.GetProfileDetailAsync(profileID, new CBSProfileConstraints());
                if (getProfileEntity.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(getProfileEntity.Error);
                }
                var profileName = getProfileEntity.Result.DisplayName;
                var chatMessage = string.Format(ClanMetaData.LeftMessageTemplate, profileName);
                var sendMessageResult = await ChatModule.SendSystemMessageToChatAsync(clanID, chatMessage);
                if (sendMessageResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(sendMessageResult.Error);
                }
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SetClanAvatarAsync(string clanID, ClanAvatarInfo avatarInfo, string profileID = null)
        {
            if (!string.IsNullOrEmpty(profileID))
            {
                var hasPermissionResult = await HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.CHANGE_META_DATA);
                if (hasPermissionResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(hasPermissionResult.Error);
                }
                var hasPermission = hasPermissionResult.Result.Value;
                if (!hasPermission)
                {
                    return ErrorHandler.NotEnoughRights<FunctionEmptyResult>();
                }
            }
            var avatarRawData = JsonPlugin.ToJson(avatarInfo);
            var internalData = new Dictionary<string, string>();
            internalData[AvatarInfoKey] = avatarRawData;
            
            var updateDataRequest = new PlayFab.ServerModels.UpdateUserInternalDataRequest {
                PlayFabId = clanID,
                Data = internalData
            };      
            var updateResult = await FabServerAPI.UpdateUserInternalDataAsync(updateDataRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateResult.Error);
            }

            var updateTableResult = await TableClanAssistant.UpdateClanAvatarAsync(clanID, avatarInfo);
            if (updateTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateTableResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SetClanDescriptionAsync(string clanID, string description, string profileID = null)
        {
            if (!string.IsNullOrEmpty(profileID))
            {
                var hasPermissionResult = await HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.CHANGE_META_DATA);
                if (hasPermissionResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(hasPermissionResult.Error);
                }
                var hasPermission = hasPermissionResult.Result.Value;
                if (!hasPermission)
                {
                    return ErrorHandler.NotEnoughRights<FunctionEmptyResult>();
                }
            }
            var internalData = new Dictionary<string, string>();
            internalData[DescriptionKey] = description;
            
            var updateDataRequest = new PlayFab.ServerModels.UpdateUserInternalDataRequest {
                PlayFabId = clanID,
                Data = internalData
            };      
            var updateResult = await FabServerAPI.UpdateUserInternalDataAsync(updateDataRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateResult.Error);
            }

            var updateTableResult = await TableClanAssistant.UpdateClanDescriptionAsync(clanID, description);
            if (updateTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateTableResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SetClanVisibilityAsync(string clanID, ClanVisibility visibility, string profileID = null)
        {
            if (!string.IsNullOrEmpty(profileID))
            {
                var hasPermissionResult = await HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.CHANGE_META_DATA);
                if (hasPermissionResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(hasPermissionResult.Error);
                }
                var hasPermission = hasPermissionResult.Result.Value;
                if (!hasPermission)
                {
                    return ErrorHandler.NotEnoughRights<FunctionEmptyResult>();
                }
            }
            var internalData = new Dictionary<string, string>();
            internalData[VisibilityKey] = visibility.ToString();
            
            var updateDataRequest = new PlayFab.ServerModels.UpdateUserInternalDataRequest {
                PlayFabId = clanID,
                Data = internalData
            };      
            var updateResult = await FabServerAPI.UpdateUserInternalDataAsync(updateDataRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateResult.Error);
            }

            var updateTableResult = await TableClanAssistant.UpdateClanVisibilityAsync(clanID, visibility);
            if (updateTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateTableResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionClanVisibilityResult>> GetClanVisibilityAsync(string clanID)
        {
            var getRawDataResult = await GetProfileInternalRawData(clanID, VisibilityKey);
            if (getRawDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionClanVisibilityResult>(getRawDataResult.Error);
            }
            var visibilityRaw = getRawDataResult.Result;
            ClanVisibility clanVisibility;
            Enum.TryParse(visibilityRaw, out clanVisibility);

            return new ExecuteResult<FunctionClanVisibilityResult>
            {
                Result = new FunctionClanVisibilityResult
                {
                    Visibility = clanVisibility
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SetClanCustomDataAsync(string clanID, Dictionary<string, string> customData)
        {    
            var updateDataRequest = new PlayFab.ServerModels.UpdateUserDataRequest {
                PlayFabId = clanID,
                Data = customData
            };      
            var updateResult = await FabServerAPI.UpdateUserDataAsync(updateDataRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateResult.Error);
            }

            var updateTableResult = await TableClanAssistant.UpdateClanDataAsync(clanID, customData);
            if (updateTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateTableResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionCustomDataResult>> GetClanCustomDataAsync(string clanID, List<string> keysToLoad = null)
        {
            var dataRequest = new PlayFab.ServerModels.GetUserDataRequest
            {
                PlayFabId = clanID,
                Keys = keysToLoad
            };
            var getInternalDataResult = await FabServerAPI.GetUserDataAsync(dataRequest);
            if (getInternalDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCustomDataResult>(getInternalDataResult.Error);
            }
            var internalData = getInternalDataResult.Result.Data ?? new Dictionary<string, PlayFab.ServerModels.UserDataRecord>();
            var clanData = internalData.ToDictionary(x=>x.Key, x=>x.Value.Value);

            return new ExecuteResult<FunctionCustomDataResult>
            {
                Result = new FunctionCustomDataResult
                {
                    ClanID = clanID,
                    CustomData = clanData
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetClanEntityResult>> GetClanEntityAsync(string clanID, CBSClanConstraints constraints)
        {
            var entityResult = await TableClanAssistant.GetClanDetailAsync(clanID, constraints);
            if (entityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanEntityResult>(entityResult.Error);
            }
            var clanEntity = entityResult.Result;

            return new ExecuteResult<FunctionGetClanEntityResult>
            {
                Result = new FunctionGetClanEntityResult
                {
                    ClanEntity = clanEntity
                }                
            };
        }

        public static async Task<ExecuteResult<FunctionGetClanEntityResult>> SearchClanByNameAsync(string clanName)
        {
            var request = new PlayFab.AdminModels.LookupUserAccountInfoRequest
            {
                TitleDisplayName = clanName
            };
            var adminAPI = await GetFabAdminAPIAsync();
            var lookupResult = await adminAPI.GetUserAccountInfoAsync(request);
            if (lookupResult.Error != null)
            {
                if (lookupResult.Error.Error == PlayFab.PlayFabErrorCode.AccountNotFound)
                {
                    return ErrorHandler.ClanNotFound<FunctionGetClanEntityResult>();
                }
                return ErrorHandler.ThrowError<FunctionGetClanEntityResult>(lookupResult.Error);
            }
            var resultInfo = lookupResult.Result.UserInfo;
            var clanID = resultInfo.PlayFabId;

            var getClanResult = await GetClanEntityAsync(clanID, CBSClanConstraints.Full());
            return getClanResult;
        }

        public static async Task<ExecuteResult<FunctionGetClanEntityResult>> GetClanOfProfileAsync(string profileID, CBSClanConstraints constraints)
        {
            var clanProfileIDResult = await ProfileModule.GetProfileClanIDAsync(profileID);
            if (clanProfileIDResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanEntityResult>(clanProfileIDResult.Error);
            }
            var clanID = clanProfileIDResult.Result;
            if (string.IsNullOrEmpty(clanID))
            {
                return ErrorHandler.ProfileIsNotMemberOfClan<FunctionGetClanEntityResult>();
            }

            var clanEntityResult = await GetClanEntityAsync(clanID, constraints);
            if (clanEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanEntityResult>(clanEntityResult.Error);
            }
            return clanEntityResult;
        }

        public static async Task<ExecuteResult<FunctionGroupIDResult>> GetClanGroupIDAsync(string clanID)
        {
            var groupIDResult = await GetProfileInternalRawData(clanID, GroupIDKey);
            if (groupIDResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGroupIDResult>(groupIDResult.Error);
            }
            var groupID = groupIDResult.Result;
            return new ExecuteResult<FunctionGroupIDResult>
            {
                Result = new FunctionGroupIDResult
                {
                    GroupID = groupID
                }
            };
        }

        public static async Task<ExecuteResult<string>> GetClanAuthIDAsync(string clanID)
        {
            var groupIDResult = await GetProfileInternalRawData(clanID, AuthDataKey);
            if (groupIDResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGroupIDResult>(groupIDResult.Error);
            }
            var authID = groupIDResult.Result;
            return new ExecuteResult<string>
            {
                Result = authID
            };
        }

        public static async Task<ExecuteResult<FunctionBoolResult>> HasMaxMembersAsync(string clanID)
        {
            var metaDataResult = await GetClanMetaDataAsync();
            if (metaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBoolResult>(metaDataResult.Error);
            }
            var metaData = metaDataResult.Result;

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBoolResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var groupApi = await GetFabGroupAPIAsync();
            var groupRequest = new ListGroupMembersRequest
            {
                Group = groupEntity
            };
            var listMemberShipResult = await groupApi.ListGroupMembersAsync(groupRequest);
            if (listMemberShipResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBoolResult>(listMemberShipResult.Error);
            }
            var roles = listMemberShipResult.Result.Members ?? new List<EntityMemberRole>();
            var membersCount = roles.Sum(x=>x.Members.Count);

            var reachLimit = membersCount >= metaData.GetClanMemberCount();

            return new ExecuteResult<FunctionBoolResult>
            {
                Result = new FunctionBoolResult
                {
                    Value = reachLimit
                }
            };
        }

        public static async Task<ExecuteResult<FunctionBoolResult>> HasPermissionForActionAsync(string clanID, string profileID, ClanRolePermission permission, ClanMetaData metaData = null)
        {
            if (metaData == null)
            {
                var metaDataResult = await GetClanMetaDataAsync();
                if (metaDataResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionBoolResult>(metaDataResult.Error);
                }
                metaData = metaDataResult.Result;
            }

            var clanGroupIDResult = await GetClanGroupIDAsync(clanID);
            if (clanGroupIDResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBoolResult>(clanGroupIDResult.Error);
            }
            var groupID = clanGroupIDResult.Result.GroupID;

            var groupApi = await GetFabGroupAPIAsync();
            var groupRequest = new ListGroupMembersRequest
            {
                Group = new PlayFab.GroupsModels.EntityKey
                {
                    Id = groupID,
                    Type = GroupEntityKey
                }
            };
            var listMemberShipResult = await groupApi.ListGroupMembersAsync(groupRequest);
            if (listMemberShipResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBoolResult>(listMemberShipResult.Error);
            }
            var members = listMemberShipResult.Result.Members ?? new List<EntityMemberRole>();

            var roleID = string.Empty;
            foreach (var memberRole in members)
            {
                foreach (var member in memberRole.Members)
                {
                    if (member.Lineage[LineageProfileAccountKey].Id == profileID)
                    {
                        roleID = memberRole.RoleId;
                        break;
                    }
                }
            }
            
            if (string.IsNullOrEmpty(roleID))
            {
                return ErrorHandler.ProfileIsNotMemberOfClan<FunctionBoolResult>();
            }

            var hasPermission = metaData.HasPermissionForAction(roleID, permission);

            return new ExecuteResult<FunctionBoolResult>
            {
                Result = new FunctionBoolResult
                {
                    Value = hasPermission
                }
            };
        }

        public static async Task<ExecuteResult<string>> GetMemberRoleIDAsync(string profileID)
        {
            var getGroupIDResult = await ProfileModule.GetProfileClanIDAsync(profileID);
            if (getGroupIDResult.Error != null)
            {
                return ErrorHandler.ThrowError<string>(getGroupIDResult.Error);
            }
            var clanID = getGroupIDResult.Result;
            if (string.IsNullOrEmpty(clanID))
            {
                return ErrorHandler.ProfileIsNotMemberOfClan<string>();
            }

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<string>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var groupApi = await GetFabGroupAPIAsync();
            var groupRequest = new ListGroupMembersRequest
            {
                Group = groupEntity
            };
            var listMemberShipResult = await groupApi.ListGroupMembersAsync(groupRequest);
            if (listMemberShipResult.Error != null)
            {
                return ErrorHandler.ThrowError<string>(listMemberShipResult.Error);
            }
            var members = listMemberShipResult.Result.Members ?? new List<EntityMemberRole>();

            var roleID = string.Empty;
            foreach (var memberRole in members)
            {
                foreach (var member in memberRole.Members)
                {
                    if (member.Lineage[LineageProfileAccountKey].Id == profileID)
                    {
                        roleID = memberRole.RoleId;
                        break;
                    }
                }
            }

            return new ExecuteResult<string>
            {
                Result = roleID
            };
        }

        public static async Task<ExecuteResult<PlayFab.GroupsModels.EntityKey>> GetClanEntityKeyAsync(string clanID)
        {
            var getRawDataResult = await GetProfileInternalRawData(clanID, GroupIDKey);
            if (getRawDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<PlayFab.GroupsModels.EntityKey>(getRawDataResult.Error);
            }
            var entityKey = getRawDataResult.Result;
            var entity = new PlayFab.GroupsModels.EntityKey
            {
                Id = entityKey,
                Type = GroupEntityKey
            };

            return new ExecuteResult<PlayFab.GroupsModels.EntityKey>
            {
                Result = entity
            };
        }

        public static async Task<ExecuteResult<FunctionInviteToClanResult>> InviteToClanAsync(string profileID, string profileToInvite)
        {
            var getClanIDResult = await ProfileModule.GetProfileClanIDAsync(profileID);
            if (getClanIDResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionInviteToClanResult>(getClanIDResult.Error);
            }
            var clanID = getClanIDResult.Result;
            if (string.IsNullOrEmpty(clanID))
            {
                return ErrorHandler.ProfileIsNotMemberOfClan<FunctionInviteToClanResult>();
            }

            var hasPermissionResult = await HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.SEND_INVITE);
            if (hasPermissionResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionInviteToClanResult>(hasPermissionResult.Error);
            }
            var hasPermission = hasPermissionResult.Result.Value;
            if (!hasPermission)
            {
                return ErrorHandler.NotEnoughRights<FunctionInviteToClanResult>();
            }

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionInviteToClanResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var profileEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileToInvite);
            if (profileEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionInviteToClanResult>(profileEntityResult.Error);
            }
            var profileEntity = profileEntityResult.Result.ToGroupEntity();

            var fabGroupAPI = await GetFabGroupAPIAsync();
            var inviteRequest = new InviteToGroupRequest
            {
                Group = groupEntity,
                Entity = profileEntity
            };
            var inviteResult = await fabGroupAPI.InviteToGroupAsync(inviteRequest);
            if (inviteResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionInviteToClanResult>(inviteResult.Error);
            }

            return new ExecuteResult<FunctionInviteToClanResult>
            {
                Result = new FunctionInviteToClanResult
                {
                    ProfileID = profileToInvite,
                    RoleId = inviteResult.Result.RoleId,
                    Expires = inviteResult.Result.Expires
                }
            };
        }

        public static async Task<ExecuteResult<ClanEntity>> GetClanEntityFromGroup(PlayFab.GroupsModels.EntityKey groupEntity, CBSClanConstraints constraints)
        {
            var fabDataAPI = await GetFabDataAPIAsync();
            var getRequest = new PlayFab.DataModels.GetObjectsRequest
            {
                Entity = groupEntity.ToDataEntity()
            };
            var objectsResult = await fabDataAPI.GetObjectsAsync(getRequest);
            if (objectsResult.Error != null)
            {
                return ErrorHandler.ThrowError<ClanEntity>(objectsResult.Error);
            }
            var dataObjects = objectsResult.Result.Objects;
            ObjectResult objResult = null;
            dataObjects.TryGetValue(GroupProfileKey, out objResult);
            if (objResult == null)
            {
                return ErrorHandler.ClanNotFound<ClanEntity>();
            }
            else
            {
                var clanID = objResult.DataObject.ToString();
                if (string.IsNullOrEmpty(clanID))
                {
                    return ErrorHandler.ClanNotFound<ClanEntity>();
                }

                var clanEntityResult = await GetClanEntityAsync(clanID, constraints);
                if (clanEntityResult.Error != null)
                {
                    return ErrorHandler.ThrowError<ClanEntity>(clanEntityResult.Error);
                }
                var clanEntity = clanEntityResult.Result.ClanEntity;

                return new ExecuteResult<ClanEntity>
                {
                    Result = clanEntity
                };
            }
        }

        public static async Task<ExecuteResult<FunctionGetInvationsResult>> GetProfileInvationsAsync(string profileID, CBSClanConstraints constraints)
        {
            var profileEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileID);
            if (profileEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetInvationsResult>(profileEntityResult.Error);
            }
            var profileEntity = profileEntityResult.Result.ToGroupEntity();

            var fabGroupAPI = await GetFabGroupAPIAsync();
            var invationsRequest = new ListMembershipOpportunitiesRequest
            {
                Entity = profileEntity
            };
            var groupInvationsResult = await fabGroupAPI.ListMembershipOpportunitiesAsync(invationsRequest);
            if (groupInvationsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetInvationsResult>(groupInvationsResult.Error);
            }
            var groupInvations = groupInvationsResult.Result.Invitations;

            var clanEntityTask = new List<Task<ExecuteResult<ClanEntity>>>();
            var invationInfoList = new List<ClanInvitationInfo>();
            foreach (var invation in groupInvations)
            {
                var groupEntity = invation.Group;
                invationInfoList.Add(new ClanInvitationInfo
                {
                    Expires = invation.Expires
                });
                clanEntityTask.Add(GetClanEntityFromGroup(groupEntity, constraints));
            }
            var entityResults = await Task.WhenAll(clanEntityTask);
            var clanEntities = entityResults.Select(x=>x.Result).ToList();

            for (int i=0;i<invationInfoList.Count;i++)
            {
                if (clanEntities[i] != null)
                {
                    invationInfoList[i].ClanID = clanEntities[i].ClanID;
                }
                invationInfoList[i].ClanEntity = clanEntities[i];
            }

            invationInfoList = invationInfoList.Where(x=>x.ClanEntity != null).ToList();

            return new ExecuteResult<FunctionGetInvationsResult>
            {
                Result = new FunctionGetInvationsResult
                {
                    Invitations = invationInfoList
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> DeclineInvationAsync(string profileID, string clanID)
        {
            var profileEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileID);
            if (profileEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(profileEntityResult.Error);
            }
            var profileEntity = profileEntityResult.Result.ToGroupEntity();

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var fabGroupAPI = await GetFabGroupAPIAsync();
            var declineRequest = new RemoveGroupInvitationRequest
            {
                Entity = profileEntity,
                Group = groupEntity
            };
            var declineResult = await fabGroupAPI.RemoveGroupInvitationAsync(declineRequest);
            if (declineResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(declineResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionAcceptInvationResult>> AcceptInvationAsync(string profileID, string clanID)
        {
            var profileEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileID);
            if (profileEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptInvationResult>(profileEntityResult.Error);
            }
            var profileEntity = profileEntityResult.Result.ToGroupEntity();

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptInvationResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var fabGroupAPI = await GetFabGroupAPIAsync();
            // check members count
            var checkMembersResult = await HasMaxMembersAsync(clanID);
            if (checkMembersResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptInvationResult>(checkMembersResult.Error);
            }
            var hasMaxMembers = checkMembersResult.Result.Value;
            if (hasMaxMembers)
            {
                return ErrorHandler.MaxMembersReached<FunctionAcceptInvationResult>();
            }

            var acceptRequest = new AcceptGroupInvitationRequest
            {
                Entity = profileEntity,
                Group = groupEntity
            };
            var acceptResult = await fabGroupAPI.AcceptGroupInvitationAsync(acceptRequest);
            if (acceptResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptInvationResult>(acceptResult.Error);
            }

            // leave current clan
            var leaveResult = await LeaveClanIfExistAsync(profileID);
            if (leaveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptInvationResult>(leaveResult.Error);
            }

            var processJoiningResult = await ProcessJoiningToClanAsync(profileID, clanID);
            if (processJoiningResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptInvationResult>(processJoiningResult.Error);
            }

            var getClanEntityResult = await GetClanEntityAsync(clanID, CBSClanConstraints.Full());
            if (getClanEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptInvationResult>(getClanEntityResult.Error);
            }
            var clanEntity = getClanEntityResult.Result.ClanEntity;

            return new ExecuteResult<FunctionAcceptInvationResult>
            {
                Result = new FunctionAcceptInvationResult
                {
                    ClanID = clanID,
                    ClanEntity = clanEntity
                }
            };
        }

        public static async Task<ExecuteResult<FunctionJoinClanResult>> JoinClanAsync(string profileID, string clanID)
        {
            var profileEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileID);
            if (profileEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionJoinClanResult>(profileEntityResult.Error);
            }
            var profileEntity = profileEntityResult.Result.ToGroupEntity();

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionJoinClanResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;
            // check clan visibility
            var getClanVisibilityResult = await GetClanVisibilityAsync(clanID);
            if (getClanVisibilityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionJoinClanResult>(getClanVisibilityResult.Error);
            }
            var clanVisibility = getClanVisibilityResult.Result.Visibility;
            if (clanVisibility == ClanVisibility.BY_REQUEST)
            {
                return ErrorHandler.ClanIsClosed<FunctionJoinClanResult>();
            }

            var fabGroupAPI = await GetFabGroupAPIAsync();
            // check members count
            var checkMembersResult = await HasMaxMembersAsync(clanID);
            if (checkMembersResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionJoinClanResult>(checkMembersResult.Error);
            }
            var hasMaxMembers = checkMembersResult.Result.Value;
            if (hasMaxMembers)
            {
                return ErrorHandler.MaxMembersReached<FunctionJoinClanResult>();
            }

            var applyRequest = new AddMembersRequest
            {
                Members = new List<PlayFab.GroupsModels.EntityKey>
                {
                    profileEntity
                },
                Group = groupEntity
            };
            var applyResult = await fabGroupAPI.AddMembersAsync(applyRequest);
            if (applyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionJoinClanResult>(applyResult.Error);
            }

            // leave current clan
            var leaveResult = await LeaveClanIfExistAsync(profileID);
            if (leaveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionJoinClanResult>(leaveResult.Error);
            }

            var processJoiningResult = await ProcessJoiningToClanAsync(profileID, clanID);
            if (processJoiningResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionJoinClanResult>(processJoiningResult.Error);
            }

            var getClanEntityResult = await GetClanEntityAsync(clanID, CBSClanConstraints.Full());
            if (getClanEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionJoinClanResult>(getClanEntityResult.Error);
            }
            var clanEntity = getClanEntityResult.Result.ClanEntity;

            return new ExecuteResult<FunctionJoinClanResult>
            {
                Result = new FunctionJoinClanResult
                {
                    ClanID = clanID,
                    ClanEntity = clanEntity
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SendJoinRequestAsync(string profileID, string clanID)
        {
            var profileEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileID);
            if (profileEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(profileEntityResult.Error);
            }
            var profileEntity = profileEntityResult.Result.ToGroupEntity();

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var fabGroupAPI = await GetFabGroupAPIAsync();
            var applyRequest = new ApplyToGroupRequest
            {
                AutoAcceptOutstandingInvite = false,
                Entity = profileEntity,
                Group = groupEntity
            };
            var applyResult = await fabGroupAPI.ApplyToGroupAsync(applyRequest);
            if (applyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(applyResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> DeclineJoinRequestAsync(string profileID, string profileIDToDecline, string clanID)
        {
            var hasPermissionResult = await HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.ACCEPT_JOIN_REQUEST);
            if (hasPermissionResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(hasPermissionResult.Error);
            }

            var profileEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileIDToDecline);
            if (profileEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(profileEntityResult.Error);
            }
            var profileEntity = profileEntityResult.Result.ToGroupEntity();

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var fabGroupAPI = await GetFabGroupAPIAsync();
            var declineRequest = new RemoveGroupApplicationRequest
            {
                Entity = profileEntity,
                Group = groupEntity
            };
            var declineResult = await fabGroupAPI.RemoveGroupApplicationAsync(declineRequest);
            if (declineResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(declineResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionAcceptJoinRequestResult>> AcceptJoinRequestAsync(string profileID, string profileIDToAccept, string clanID)
        {
            var hasPermissionResult = await HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.ACCEPT_JOIN_REQUEST);
            if (hasPermissionResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptJoinRequestResult>(hasPermissionResult.Error);
            }
            var hasPermission = hasPermissionResult.Result.Value;
            if (!hasPermission)
            {
                return ErrorHandler.NotEnoughRights<FunctionAcceptJoinRequestResult>();
            }

            var profileEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileIDToAccept);
            if (profileEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptJoinRequestResult>(profileEntityResult.Error);
            }
            var profileEntity = profileEntityResult.Result.ToGroupEntity();

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptJoinRequestResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var fabGroupAPI = await GetFabGroupAPIAsync();
            // check members count
            var checkMembersResult = await HasMaxMembersAsync(clanID);
            if (checkMembersResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptJoinRequestResult>(checkMembersResult.Error);
            }
            var hasMaxMembers = checkMembersResult.Result.Value;
            if (hasMaxMembers)
            {
                return ErrorHandler.MaxMembersReached<FunctionAcceptJoinRequestResult>();
            }

            var acceptRequest = new AcceptGroupApplicationRequest
            {
                Entity = profileEntity,
                Group = groupEntity
            };
            var acceptResult = await fabGroupAPI.AcceptGroupApplicationAsync(acceptRequest);
            if (acceptResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptJoinRequestResult>(acceptResult.Error);
            }

            // leave current clan
            var leaveResult = await LeaveClanIfExistAsync(profileIDToAccept);
            if (leaveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptJoinRequestResult>(leaveResult.Error);
            }

            var processJoiningResult = await ProcessJoiningToClanAsync(profileIDToAccept, clanID);
            if (processJoiningResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptJoinRequestResult>(processJoiningResult.Error);
            }

            var memberEntityResult = await TableProfileAssistant.GetProfileDetailAsync(profileIDToAccept, CBSProfileConstraints.Full());
            if (memberEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionAcceptJoinRequestResult>(memberEntityResult.Error);
            }
            var memberEntity = memberEntityResult.Result;

            return new ExecuteResult<FunctionAcceptJoinRequestResult>
            {
                Result = new FunctionAcceptJoinRequestResult
                {
                    ProfileEntity = memberEntity
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetJoinRequestListResult>> GetJoinRequestListAsync(string clanID, CBSProfileConstraints constraints)
        {
            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetJoinRequestListResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var fabGroupAPI = await GetFabGroupAPIAsync();
            var applicationResult = new ListGroupApplicationsRequest
            {
                Group = groupEntity
            };
            var listApplicationResult = await fabGroupAPI.ListGroupApplicationsAsync(applicationResult);
            if (listApplicationResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetJoinRequestListResult>(listApplicationResult.Error);
            }
            var applications = listApplicationResult.Result.Applications ?? new List<GroupApplication>();

            var requestList = new List<ClanRequestInfo>();
            foreach (var application in applications)
            {
                var profileID = application.Entity.Lineage[LineageProfileAccountKey].Id;
                var expires = application.Expires;
                requestList.Add(new ClanRequestInfo
                {
                    ProfileID = profileID,
                    Expires = expires
                });
            }

            var profileEntitiesResult = await TableProfileAssistant.GetProfilesDetailsAsync(requestList.Select(x=>x.ProfileID).ToArray(), constraints);
            if (profileEntitiesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetJoinRequestListResult>(profileEntitiesResult.Error);
            }
            var profileEntities = profileEntitiesResult.Result;

            foreach (var request in requestList)
            {
                var profileID = request.ProfileID;
                request.ProfileEntity = profileEntities.ContainsKey(profileID) ? profileEntities[profileID] : new ProfileEntity();
            }

            return new ExecuteResult<FunctionGetJoinRequestListResult>
            {
                Result = new FunctionGetJoinRequestListResult
                {
                    RequestList = requestList
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> LeaveClanAsync(string profileID, string clanID)
        {
            var profileEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileID);
            if (profileEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(profileEntityResult.Error);
            }
            var profileEntity = profileEntityResult.Result.ToGroupEntity();

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var fabGroupAPI = await GetFabGroupAPIAsync();

            var membersToRemove = new List<PlayFab.GroupsModels.EntityKey>();
            membersToRemove.Add(profileEntity);

            var removeRequest = new RemoveMembersRequest
            {
                Group = groupEntity,
                Members = membersToRemove
            };

            var removeResult = await fabGroupAPI.RemoveMembersAsync(removeRequest);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(removeResult.Error);
            }

            var leaveProcessResult = await ProcessLeaveClanAsync(profileID, clanID);
            if (leaveProcessResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(leaveProcessResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionGetClanFullInfoResult>> GetClanFullInfoAsync(string clanID)
        {
            var metaDataResult = await GetClanMetaDataAsync();
            if (metaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanFullInfoResult>(metaDataResult.Error);
            }
            var metaData = metaDataResult.Result;

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanFullInfoResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var internalDataRequest = new PlayFab.ServerModels.GetUserDataRequest
            {
                PlayFabId = clanID
            };
            var getInternalDataResult = await FabServerAPI.GetUserInternalDataAsync(internalDataRequest);
            if (getInternalDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanFullInfoResult>(getInternalDataResult.Error);
            }
            var internalData = getInternalDataResult.Result.Data ?? new Dictionary<string, PlayFab.ServerModels.UserDataRecord>();

            var description = internalData.ContainsKey(DescriptionKey) ? internalData[DescriptionKey].Value : string.Empty;
            var groupID = internalData.ContainsKey(GroupIDKey) ? internalData[GroupIDKey].Value : string.Empty;
            var visibilityRaw = internalData.ContainsKey(VisibilityKey) ? internalData[VisibilityKey].Value : string.Empty;
            ClanVisibility visibility;
            Enum.TryParse(visibilityRaw, out visibility);
            var avatarRaw = internalData.ContainsKey(AvatarInfoKey) ? internalData[AvatarInfoKey].Value : JsonPlugin.EMPTY_JSON;
            var avatarInfo = JsonPlugin.FromJson<ClanAvatarInfo>(avatarRaw);

            var getDisplayNameResult = await GetClanDisplayNameAsync(clanID);
            if (getDisplayNameResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanFullInfoResult>(getDisplayNameResult.Error);
            }
            var displayName = getDisplayNameResult.Result.DisplayName;

            var getMemberCountResult = await GetClanMemberCountAsync(clanID);
            if (getMemberCountResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanFullInfoResult>(getMemberCountResult.Error);
            }
            var clanMembersCount = getMemberCountResult.Result.Value;

            var rolesResult = await GetAvailableRolesForClanAsync(clanID, metaData);
            if (rolesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanFullInfoResult>(rolesResult.Error);
            }
            var availableRoles = rolesResult.Result;

            var getCustomDataResult = await GetClanCustomDataAsync(clanID);
            if (getCustomDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanFullInfoResult>(getCustomDataResult.Error);
            }
            var customData = getCustomDataResult.Result.CustomData;

            var levelInfoResult = await ClanExpModule.GetClanExpirienceDetailAsync(clanID);
            if (levelInfoResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanFullInfoResult>(levelInfoResult.Error);
            }
            var levelInfo = levelInfoResult.Result;

            var clanInfo = new ClanFullInfo
            {
                ClanID = clanID,
                ClanGroupID = groupID,
                DisplayName = displayName,
                Description = description,
                MembersCount = clanMembersCount,
                AvatarInfo = avatarInfo,
                Visibility = visibility,
                RolesList = availableRoles,
                CustomData = customData,
                LevelInfo = levelInfo
            };

            return new ExecuteResult<FunctionGetClanFullInfoResult>
            {
                Result = new FunctionGetClanFullInfoResult
                {
                    Info = clanInfo
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetClanBadgeResult>> GetClanBadgeAsync(string profileID)
        {
            var getClanIDResult = await ProfileModule.GetProfileClanIDAsync(profileID);
            if (getClanIDResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanBadgeResult>(getClanIDResult.Error);
            }
            var clanID = getClanIDResult.Result;
            var fabGroupAPI = await GetFabGroupAPIAsync();
            var requestsCount = 0;
            var invationsCount = 0;

            var existInClan = !string.IsNullOrEmpty(clanID);
            if (existInClan)
            {
                var groupEntityResult = await GetClanEntityKeyAsync(clanID);
                if (groupEntityResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGetClanBadgeResult>(groupEntityResult.Error);
                }
                var groupEntity = groupEntityResult.Result;

                var applicationResult = new ListGroupApplicationsRequest
                {
                    Group = groupEntity
                };
                var listApplicationResult = await fabGroupAPI.ListGroupApplicationsAsync(applicationResult);
                if (listApplicationResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGetClanBadgeResult>(listApplicationResult.Error);
                }
                var applications = listApplicationResult.Result.Applications ?? new List<GroupApplication>();
                requestsCount = applications.Count;
            }

            var profileEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileID);
            if (profileEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanBadgeResult>(profileEntityResult.Error);
            }
            var profileEntity = profileEntityResult.Result.ToGroupEntity();

            var invationsRequest = new ListMembershipOpportunitiesRequest
            {
                Entity = profileEntity
            };
            var groupInvationsResult = await fabGroupAPI.ListMembershipOpportunitiesAsync(invationsRequest);
            if (groupInvationsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanBadgeResult>(groupInvationsResult.Error);
            }
            var groupInvations = groupInvationsResult.Result.Invitations ?? new List<GroupInvitation>();

            invationsCount = groupInvations.Count;

            return new ExecuteResult<FunctionGetClanBadgeResult>
            {
                Result = new FunctionGetClanBadgeResult
                {
                    RequestsCount = requestsCount,
                    InvationsCount = invationsCount
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGetClanMembersResult>> GetClanMembersAsync(string clanID, CBSProfileConstraints constraints)
        {
            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanMembersResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var availableRolesResult = await GetAvailableRolesForClanAsync(clanID);
            if (availableRolesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanMembersResult>(availableRolesResult.Error);
            }
            var availableRoles = availableRolesResult.Result;
            var roleDictionary = availableRoles.ToDictionary(x=>x.RoleID, x=>x);

            var groupApi = await GetFabGroupAPIAsync();
            var groupRequest = new ListGroupMembersRequest
            {
                Group = groupEntity
            };
            var listMemberShipResult = await groupApi.ListGroupMembersAsync(groupRequest);
            if (listMemberShipResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanMembersResult>(listMemberShipResult.Error);
            }
            var membersWithRoles = listMemberShipResult.Result.Members ?? new List<EntityMemberRole>();
            var clanMembers = new List<ClanMember>();
            foreach (var memberRole in membersWithRoles)
            {
                foreach (var member in memberRole.Members)
                {
                    var profileID = member.Lineage[LineageProfileAccountKey].Id;
                    var roleID = memberRole.RoleId;
                    var roleName = roleDictionary.ContainsKey(roleID) ? roleDictionary[roleID].DisplayName : UnknownRoleName;
                    clanMembers.Add(new ClanMember
                    {
                        ProfileID = profileID,
                        RoleID = roleID,
                        RoleName = roleName
                    });
                }
            }

            var getEntitiesResult = await TableProfileAssistant.GetProfilesDetailsAsync(clanMembers.Select(x=>x.ProfileID).ToArray(), constraints);
            if (getEntitiesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetClanMembersResult>(getEntitiesResult.Error);
            }
            var entities = getEntitiesResult.Result;

            foreach (var clanMember in clanMembers)
                clanMember.ProfileEntity = entities[clanMember.ProfileID];

            return new ExecuteResult<FunctionGetClanMembersResult>
            {
                Result = new FunctionGetClanMembersResult
                {
                    AvailableRoles = availableRoles,
                    Members = clanMembers
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> KickMemberAsync(string profileID, string profileIDToKick, string clanID)
        {
            var hasPermissionResult = await HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.KICK_MEMBER);
            if (hasPermissionResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(hasPermissionResult.Error);
            }
            var hasPermission = hasPermissionResult.Result.Value;
            if (!hasPermission)
            {
                return ErrorHandler.NotEnoughRights<FunctionEmptyResult>();
            }

            var leaveResult = await LeaveClanAsync(profileIDToKick, clanID);
            if (leaveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(leaveResult.Error);
            }
            
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionChangeClanRoleResult>> ChangeMemberRoleAsync(string profileID, string profileIDToChange, string clanID, string roleID)
        {
            var hasPermissionResult = await HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.CHANGE_ROLE);
            if (hasPermissionResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChangeClanRoleResult>(hasPermissionResult.Error);
            }
            var hasPermission = hasPermissionResult.Result.Value;
            if (!hasPermission)
            {
                return ErrorHandler.NotEnoughRights<FunctionChangeClanRoleResult>();
            }

            var getProfileRoleIDResult = await GetMemberRoleIDAsync(profileIDToChange);
            if (getProfileRoleIDResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChangeClanRoleResult>(getProfileRoleIDResult.Error);
            }
            var memberRoleID = getProfileRoleIDResult.Result;

            var getClanIDResult = await ProfileModule.GetProfileClanIDAsync(profileIDToChange);
            if (getClanIDResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChangeClanRoleResult>(getClanIDResult.Error);
            }
            var memberClanID = getClanIDResult.Result;
            if (memberClanID != clanID)
            {
                return ErrorHandler.ProfileIsNotMemberOfClan<FunctionChangeClanRoleResult>();
            }

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChangeClanRoleResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var memberEntityResult = await ProfileModule.GetProfileEntityKeyAsync(profileIDToChange);
            if (memberEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChangeClanRoleResult>(memberEntityResult.Error);
            }
            var memberEntity = memberEntityResult.Result.ToGroupEntity();

            var fabGroupAPI = await GetFabGroupAPIAsync();
            var changeRequest = new ChangeMemberRoleRequest
            {
                DestinationRoleId = roleID,
                OriginRoleId = memberRoleID,
                Group = groupEntity,
                Members = new List<PlayFab.GroupsModels.EntityKey>
                {
                    memberEntity
                }
            };
            var changeResult = await fabGroupAPI.ChangeMemberRoleAsync(changeRequest);
            if (changeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChangeClanRoleResult>(changeResult.Error);
            }

            var metaDataResult = await GetClanMetaDataAsync();
            if (metaDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChangeClanRoleResult>(metaDataResult.Error);
            }
            var metaData = metaDataResult.Result;
            var roles = metaData.GetRoleList();
            var roleInfo = roles.FirstOrDefault(x=>x.RoleID == roleID);

            var sendChangeMessage = metaData.SendChangeRoleMessage;
            if (sendChangeMessage)
            {
                var getProfileEntity = await TableProfileAssistant.GetProfileDetailAsync(profileID, new CBSProfileConstraints());
                if (getProfileEntity.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionChangeClanRoleResult>(getProfileEntity.Error);
                }
                var profileName = getProfileEntity.Result.DisplayName;
                var chatMessage = string.Format(ClanMetaData.RoleMessageTemplate, profileName, roleInfo.DisplayName);
                var sendMessageResult = await ChatModule.SendSystemMessageToChatAsync(clanID, chatMessage);
                if (sendMessageResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionChangeClanRoleResult>(sendMessageResult.Error);
                }
            }

            return new ExecuteResult<FunctionChangeClanRoleResult>
            {
                Result = new FunctionChangeClanRoleResult
                {
                    NewRole = roleInfo,
                    ProfileID = profileIDToChange
                }
            };
        }
        
        public static async Task<ExecuteResult<FunctionEmptyResult>> DisbandClanAsync(string profileID, string clanID, bool forceDisband = false)
        {
            // check permission
            if (!forceDisband)
            {
                var hasPermissionResult = await HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.DISBAND_CLAN);
                if (hasPermissionResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(hasPermissionResult.Error);
                }
                var hasPermission = hasPermissionResult.Result.Value;
                if (!hasPermission)
                {
                    return ErrorHandler.NotEnoughRights<FunctionEmptyResult>();
                }

                // check valid clan id
                var profileClanIDResult = await ProfileModule.GetProfileClanIDAsync(profileID);
                if (profileClanIDResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionEmptyResult>(profileClanIDResult.Error);
                }
                var profileClanID = profileClanIDResult.Result;
                if (string.IsNullOrEmpty(profileClanID) || profileClanID != clanID)
                {
                    return ErrorHandler.ProfileIsNotMemberOfClan<FunctionEmptyResult>();
                }
            }

            // get members
            var getClanMembersResult = await GetClanMembersAsync(clanID, new CBSProfileConstraints());
            if (getClanMembersResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(getClanMembersResult.Error);
            }
            var members = getClanMembersResult.Result.Members;

            // get group entity
            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            // delete group
            var fabGroupAPI = await GetFabGroupAPIAsync();
            var deleteGroupRequest = new DeleteGroupRequest
            {
                Group = groupEntity
            };
            var deleteGroupResult = await fabGroupAPI.DeleteGroupAsync(deleteGroupRequest);
            if (deleteGroupResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(deleteGroupResult.Error);
            }

            // delete clan profile
            var fabAdminApi = await GetFabAdminAPIAsync();
            var deleteProfileRequest = new PlayFab.AdminModels.DeleteMasterPlayerAccountRequest
            {
                PlayFabId = clanID
            };
            var deleteProfileResult = await fabAdminApi.DeleteMasterPlayerAccountAsync(deleteProfileRequest);
            if (deleteProfileResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(deleteProfileResult.Error);
            }

            // delete clan from table data
            var deleteEntityResult = await TableClanAssistant.DeleteClanAsync(clanID);
            if (deleteEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(deleteEntityResult.Error);
            }

            // clear all members
            var cleanTasks = new List<Task<ExecuteResult<FunctionEmptyResult>>>();
            foreach (var member in members)
            {
                cleanTasks.Add(ProfileModule.SetProfileClanIDAsync(member.ProfileID, string.Empty));
            }
            await Task.WhenAll(cleanTasks);

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionBoolResult>> CheckIfProfileIsClanAsync(string profileID)
        {
            var getGroupIDResult = await GetClanGroupIDAsync(profileID);
            if (getGroupIDResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBoolResult>(getGroupIDResult.Error);
            }
            var groupID = getGroupIDResult.Result.GroupID;
            var isClanProfile = !string.IsNullOrEmpty(groupID);

            return new ExecuteResult<FunctionBoolResult>
            {
                Result = new FunctionBoolResult
                {
                    Value = isClanProfile
                }
            };
        }

        private static async Task<ExecuteResult<FunctionIntResult>> GetClanMemberCountAsync(string clanID)
        {
            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionIntResult>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var groupApi = await GetFabGroupAPIAsync();
            var groupRequest = new ListGroupMembersRequest
            {
                Group = groupEntity
            };
            var listMemberShipResult = await groupApi.ListGroupMembersAsync(groupRequest);
            if (listMemberShipResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionIntResult>(listMemberShipResult.Error);
            }
            var roles = listMemberShipResult.Result.Members ?? new List<EntityMemberRole>();
            var membersCount = roles.Sum(x=>x.Members.Count);

            return new ExecuteResult<FunctionIntResult>
            {
                Result = new FunctionIntResult
                {
                    Value = membersCount
                }
            };
        }

        private static async Task<ExecuteResult<FunctionEmptyResult>> RecalculateClanMembersCountAsync(string clanID)
        {
            var memberCountResult = await GetClanMemberCountAsync(clanID);
            if (memberCountResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(memberCountResult.Error);
            }
            var memberCount = memberCountResult.Result.Value;

            var tableResult = await TableClanAssistant.UpdateClanMembersCountAsync(clanID, memberCount);
            if (tableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(tableResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        private static async Task<ExecuteResult<List<ClanRoleInfo>>> GetAvailableRolesForClanAsync(string clanID, ClanMetaData metaData = null)
        {
            if (metaData == null)
            {
                var metaDataResult = await GetClanMetaDataAsync();
                if (metaDataResult.Error != null)
                {
                    return ErrorHandler.ThrowError<List<ClanRoleInfo>>(metaDataResult.Error);
                }
                metaData = metaDataResult.Result;
            }

            var groupEntityResult = await GetClanEntityKeyAsync(clanID);
            if (groupEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<ClanRoleInfo>>(groupEntityResult.Error);
            }
            var groupEntity = groupEntityResult.Result;

            var fabGroupAPI = await GetFabGroupAPIAsync();
            var groupRequest = new GetGroupRequest
            {
                Group = groupEntity
            };
            var getGroupResult = await fabGroupAPI.GetGroupAsync(groupRequest);
            if (getGroupResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<ClanRoleInfo>>(getGroupResult.Error);
            }
            var groupInfo = getGroupResult.Result;
            var groupRoles = groupInfo.Roles;
            var allRoles = metaData.GetRoleList();
            var availableRoles = allRoles.Where(x=> groupRoles.ContainsKey(x.RoleID)).ToList();

            return new ExecuteResult<List<ClanRoleInfo>>
            {
                Result = availableRoles
            };
        }
    }
}