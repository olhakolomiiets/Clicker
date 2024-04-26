using PlayFab.ServerModels;
using PlayFab.AdminModels;
using PlayFab.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CBS.Models;
using System.Linq;
using System.Collections.Generic;
using EmptyResponse = PlayFab.ServerModels.EmptyResponse;
using GetUserDataRequest = PlayFab.ServerModels.GetUserDataRequest;
using GetUserDataResult = PlayFab.ServerModels.GetUserDataResult;
using UpdateUserDataRequest = PlayFab.ServerModels.UpdateUserDataRequest;
using UpdateUserDataResult = PlayFab.ServerModels.UpdateUserDataResult;
using BanUsersRequest = PlayFab.ServerModels.BanUsersRequest;
using BanUsersResult = PlayFab.ServerModels.BanUsersResult;
using BanRequest = PlayFab.ServerModels.BanRequest;
using RevokeAllBansForUserRequest = PlayFab.ServerModels.RevokeAllBansForUserRequest;
using RevokeAllBansForUserResult = PlayFab.ServerModels.RevokeAllBansForUserResult;
using RevokeBansRequest = PlayFab.ServerModels.RevokeBansRequest;
using RevokeBansResult = PlayFab.ServerModels.RevokeBansResult;
using GetUserBansRequest = PlayFab.ServerModels.GetUserBansRequest;
using GetUserBansResult = PlayFab.ServerModels.GetUserBansResult;

namespace CBS
{
    public class ProfileModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.UpdateProfileDisplayNameMethod)]
        public static async Task<dynamic> UpdateProfileDisplayNameTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateDisplayNameRequest>();

            var profileID = request.ProfileID;
            var name = request.DisplayName;
            var profanityCheck = request.ProfanityCheck;

            var updateResult = await SetProfileDisplayNameAsync(profileID, name, profanityCheck);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddExperienceToPlayerMethod)]
        public static async Task<dynamic> AddExpirienceToPlayerTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionAddExpRequest>();

            var profileID = request.ProfileID;
            var expToAdd = request.ExpToAdd;

            var addResult = await ProfileExpModule.AddExpirienceToPlayerAsync(profileID, expToAdd);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError(addResult.Error).AsFunctionResult();
            }

            return addResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileExperienceMethod)]
        public static async Task<dynamic> GetProfileExpirienceDetailTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var profileID = request.ProfileID;

            var getResult = await ProfileExpModule.GetProfileExpirienceDetailAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileDetailByDisplayNameMethod)]
        public static async Task<dynamic> GetProfileDetailByDisplayNameTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetProfileDetailRequest>();

            var displayName = request.DisplayName;
            var constraints = request.Constraints == null ? new CBSProfileConstraints() : request.Constraints;

            var profileResult = await GetProfileDetailByDisplayNameAsync(displayName, constraints);
            if (profileResult.Error != null)
            {
                return ErrorHandler.ThrowError(profileResult.Error).AsFunctionResult();
            }
            var profileDetail = profileResult.Result;

            return profileDetail.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileDetailMethod)]
        public static async Task<dynamic> GetProfileDetailTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetProfileDetailRequest>();

            var profileID = request.ProfileID;
            var constraints = request.Constraints == null ? new CBSProfileConstraints() : request.Constraints;

            var profileResult = await TableProfileAssistant.GetProfileDetailAsync(profileID, constraints);
            if (profileResult.Error != null)
            {
                return ErrorHandler.ThrowError(profileResult.Error).AsFunctionResult();
            }
            var profileDetail = profileResult.Result;

            return profileDetail.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfilesDetailsMethod)]
        public static async Task<dynamic> GetProfilesDetailsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetProfilesDetailsRequest>();

            var profilesIDs = request.ProfilesIDs;
            var constraints = request.Constraints == null ? new CBSProfileConstraints() : request.Constraints;

            var profileResult = await TableProfileAssistant.GetProfilesDetailsAsync(profilesIDs, constraints);
            if (profileResult.Error != null)
            {
                return ErrorHandler.ThrowError(profileResult.Error).AsFunctionResult();
            }
            var profilesDetail = profileResult.Result;

            return new FunctionProfilesResult
            {
                Profiles = profileResult.Result
            }.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateProfileImageURLMethod)]
        public static async Task<dynamic> UpdateProfileImageURLTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateURLRequest>();

            var profileID = request.ProfileID;
            var url = request.URL;

            var updateResult = await SetProfileAvatarImageUrlAsync(profileID, url);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }
            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileLevelTableMethod)]
        public static async Task<dynamic> GetProfileLevelTableTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionKeyRequest>();

            var getResult = await ProfileExpModule.GetLevelTableAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }
            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfilesDataMethod)]
        public static async Task<dynamic> GetProfilesDataTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionKeysRequest>();

            var profileID = request.ProfileID;
            var keys = request.Keys;

            var getResult = await GetProfileDataAsync(profileID, keys);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SetProfilesDataMethod)]
        public static async Task<dynamic> SetProfilesDataTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionKeyPairRequest>();

            var profileID = request.ProfileID;
            var key = request.Key;
            var value = request.Value;

            var getResult = await SetProfileDataAsync(profileID, key, value);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SetProfilesMultiplyDataMethod)]
        public static async Task<dynamic> SetProfilesMultiplyDataTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionDictionaryRequest>();

            var profileID = request.ProfileID;
            var dataSet = request.Dictionary;

            var getResult = await SetProfileDataAsync(profileID, dataSet);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateProfileOnlineStateMethod)]
        public static async Task<dynamic> UpdateProfileOnlineStateTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionUpdateOnlineRequest>();

            var profileID = request.ProfileID;
            var threshold = request.OnlineThreshold;

            var updateResult = await TableProfileAssistant.UpdateProfileOnlineStateAsync(profileID, threshold);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }
            return new EmptyResponse().AsFunctionResult();
        }

        [FunctionName(AzureFunctions.BanProfileMethod)]
        public static async Task<dynamic> BanProfileTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBanProfileRequest>();

            var profileID = request.ProfileID;
            var reason = request.Reason;
            var hours = request.Hours;

            var banResult = await BanProfileAsync(profileID, reason, hours);
            if (banResult.Error != null)
            {
                return ErrorHandler.ThrowError(banResult.Error).AsFunctionResult();
            }
            var banData = banResult.Result.BanData.FirstOrDefault();
            var banDetail = banData.ToEntityBan();
            return banDetail.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.RevokeAllBansProfileMethod)]
        public static async Task<dynamic> RevokeAllBansTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var profileID = request.ProfileID;

            var revokeResult = await RevokeAllProfileBansAsync(profileID);
            if (revokeResult.Error != null)
            {
                return ErrorHandler.ThrowError(revokeResult.Error).AsFunctionResult();
            }
            return revokeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.RevokeBanProfileMethod)]
        public static async Task<dynamic> RevokeProfileBanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionRevokeBanRequest>();

            var banID = request.BanID;

            var revokeResult = await RevokeProfileBanAsync(banID);
            if (revokeResult.Error != null)
            {
                return ErrorHandler.ThrowError(revokeResult.Error).AsFunctionResult();
            }
            return revokeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileBansMethod)]
        public static async Task<dynamic> GetProfileBansTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var profileID = request.ProfileID;

            var revokeResult = await GetProfileBansAsync(profileID);
            if (revokeResult.Error != null)
            {
                return ErrorHandler.ThrowError(revokeResult.Error).AsFunctionResult();
            }
            var banList = revokeResult.Result.BanData;
            var cbsList = banList.Select(x=>x.ToEntityBan()).ToList();
            var banResult = new FunctionBanResult
            {
                BanList = cbsList
            };
            return banResult.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.UpdateProfileAvatarIDMethod)]
        public static async Task<dynamic> UpdateProfileAvatarIDTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();

            var avatarID = request.ID;
            var profileID = request.ProfileID;

            var saveResult = await SaveProfileAvatarIDAsync(profileID, avatarID);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError(saveResult.Error).AsFunctionResult();
            }
            return saveResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileAvatarIDMethod)]
        public static async Task<dynamic> GetProfileAvatarIDTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var profileID = request.ProfileID;

            var getResult = await GetProfileAvatarIDAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }
            return getResult.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileAvatarTableMethod)]
        public static async Task<dynamic> GetProfileAvatarTableTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var getResult = await GetAvatarTableAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }
            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetProfileAvatarTableWithStatesMethod)]
        public static async Task<dynamic> GetProfileAvatarTableWithStatesTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();
            var profileID = request.ProfileID;

            var getResult = await GetAvatarTableWithStatesAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }
            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PurchaseProfileAvatarMethod)]
        public static async Task<dynamic> PurchaseProfileAvatarTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var avatarID = request.ID;

            var purchaseResult = await PurchaseAvatarAsync(profileID, avatarID);
            if (purchaseResult.Error != null)
            {
                return ErrorHandler.ThrowError(purchaseResult.Error).AsFunctionResult();
            }
            return purchaseResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GrantProfileAvatarMethod)]
        public static async Task<dynamic> GrantProfileAvatarTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var avatarID = request.ID;

            var grantResult = await GrantAvatarAsync(profileID, avatarID);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError(grantResult.Error).AsFunctionResult();
            }
            return grantResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.DeleteMasterPlayerAccountMethod)]
        public static async Task<dynamic> DeleteMasterPlayerAccountTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            var profileID = context.CallerEntityProfile.Lineage.MasterPlayerAccountId;

            var deleteResult = await DeleteMasterPlayerAccountAsync(profileID);
            if (deleteResult.Error != null)
            {
                return ErrorHandler.ThrowError(deleteResult.Error).AsFunctionResult();
            }

            return deleteResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<EmptyResponse>> SetProfileAvatarImageUrlAsync(string profileID, string url)
        {
            var updateRequest = new UpdateAvatarUrlRequest
            {
                PlayFabId = profileID,
                ImageUrl = url
            };
            var updateResult = await FabServerAPI.UpdateAvatarUrlAsync(updateRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<EmptyResponse>(updateResult.Error);
            }
            var tableResult = await TableProfileAssistant.UpdateProfileAvatarURLAsync(profileID, url);
            if (tableResult.Error != null)
            {
                return ErrorHandler.ThrowError<EmptyResponse>(tableResult.Error);
            }
            return new ExecuteResult<EmptyResponse>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<UpdateUserTitleDisplayNameResult>> SetProfileDisplayNameAsync(string profileID, string displayName, bool profanityCheck  = false)
        {
            if (profanityCheck)
            {
                var hasCensoredWord = Censor.HasCensoredWord(displayName);
                if (hasCensoredWord)
                {
                    return ErrorHandler.InvalidInput<UpdateUserTitleDisplayNameResult>();
                }
            }
            var request = new UpdateUserTitleDisplayNameRequest
            {
                PlayFabId = profileID,
                DisplayName = displayName
            };
            var adminAPI = await GetFabAdminAPIAsync();
            var result = await adminAPI.UpdateUserTitleDisplayNameAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<UpdateUserTitleDisplayNameResult>(result.Error);
            }

            var tableResult = await TableProfileAssistant.UpdateProfileDisplayNameAsync(profileID, displayName);
            if (tableResult.Error != null)
            {
                return ErrorHandler.ThrowError<UpdateUserTitleDisplayNameResult>(tableResult.Error);
            }

            return new ExecuteResult<UpdateUserTitleDisplayNameResult>
            {
                Result = result.Result
            };
        }

        public static async Task<ExecuteResult<GetUserDataResult>> GetProfileDataAsync(string profileID, string [] keys)
        {
            var request = new GetUserDataRequest
            {
                PlayFabId = profileID,
                Keys = keys.ToList()
            };
            var getResult = await FabServerAPI.GetUserDataAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError<GetUserDataResult>(getResult.Error);
            }
            return new ExecuteResult<GetUserDataResult>
            {
                Result = getResult.Result
            };
        }

        public static async Task<ExecuteResult<UpdateUserDataResult>> SetProfileDataAsync(string profileID, string key, string data)
        {
            var requestData = new Dictionary<string, string>();
            requestData[key] = data;
            var request = new UpdateUserDataRequest
            {
                PlayFabId = profileID,
                Data = requestData
            };
            var updateResult = await FabServerAPI.UpdateUserDataAsync(request);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<UpdateUserDataResult>(updateResult.Error);
            }

            // update with azure table
            var tableResult = await TableProfileAssistant.UpdateProfileDataAsync(profileID, key, data);
            if (tableResult.Error != null)
            {
                return ErrorHandler.ThrowError<UpdateUserDataResult>(tableResult.Error);
            }

            return new ExecuteResult<UpdateUserDataResult>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<UpdateUserDataResult>> SetProfileDataAsync(string profileID, Dictionary<string, string> requestData)
        {
            var request = new UpdateUserDataRequest
            {
                PlayFabId = profileID,
                Data = requestData
            };
            var updateResult = await FabServerAPI.UpdateUserDataAsync(request);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<UpdateUserDataResult>(updateResult.Error);
            }

            // update with azure table
            var tasksList = new List<Task<ExecuteResult<Azure.Response>>>();
            foreach (var dataPair in requestData)
            {
                tasksList.Add(TableProfileAssistant.UpdateProfileDataAsync(profileID, dataPair.Key, dataPair.Value));
            }
            var tableResult = await Task.WhenAll(tasksList);

            return new ExecuteResult<UpdateUserDataResult>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<BanUsersResult>> BanProfileAsync(string profileID, string reason, uint hours)
        {
            var banRequest = new BanRequest
            {
                PlayFabId = profileID,
                Reason = reason,
                DurationInHours = hours
            };
            var banRequests = new List<BanRequest>();
            banRequests.Add(banRequest);
            var request = new BanUsersRequest
            {
                Bans = banRequests
            };

            var result = await FabServerAPI.BanUsersAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<BanUsersResult>(result.Error);
            }
            return new ExecuteResult<BanUsersResult>
            {
                Result = result.Result
            };
        }

        public static async Task<ExecuteResult<RevokeAllBansForUserResult>> RevokeAllProfileBansAsync(string profileID)
        {
            var request = new RevokeAllBansForUserRequest
            {
                PlayFabId = profileID
            };
            var revokeResult = await FabServerAPI.RevokeAllBansForUserAsync(request);
            if (revokeResult.Error != null)
            {
                return ErrorHandler.ThrowError<RevokeAllBansForUserResult>(revokeResult.Error);
            }
            return new ExecuteResult<RevokeAllBansForUserResult>
            {
                Result = revokeResult.Result
            };
        }

        public static async Task<ExecuteResult<RevokeBansResult>> RevokeProfileBanAsync(string banID)
        {
            var bansIDs = new List<string>();
            bansIDs.Add(banID);
            var request = new RevokeBansRequest
            {
                BanIds = bansIDs
            };
            var revokeResult = await FabServerAPI.RevokeBansAsync(request);
            if (revokeResult.Error != null)
            {
                return ErrorHandler.ThrowError<RevokeBansResult>(revokeResult.Error);
            }
            return new ExecuteResult<RevokeBansResult>
            {
                Result = revokeResult.Result
            };
        }

        public static async Task<ExecuteResult<GetUserBansResult>> GetProfileBansAsync(string profileID)
        {
            var request = new GetUserBansRequest
            {
                PlayFabId = profileID
            };
            var getResult = await FabServerAPI.GetUserBansAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError<GetUserBansResult>(getResult.Error);
            }
            return new ExecuteResult<GetUserBansResult>
            {
                Result = getResult.Result
            };
        }

        public static async Task<ExecuteResult<string>> GetProfileAvatarIDAsync(string profileID)
        {
            var dataResult = await BaseAzureModule.GetProfileInternalRawData(profileID, ProfileDataKeys.AvatarID);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<string>(dataResult.Error);
            }
            return new ExecuteResult<string>
            {
                Result = dataResult.Result
            };
        }

        public static async Task<ExecuteResult<FunctionUpdateAvatarIDResult>> SaveProfileAvatarIDAsync(string profileID, string avatarID)
        {
            var statesResult = await GetAvatarTableWithStatesAsync(profileID);
            if (statesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateAvatarIDResult>(statesResult.Error);
            }
            var avatarTableResult = statesResult.Result;
            var states = avatarTableResult.AvatarStates;
            var avatarState = states.FirstOrDefault(x=>x.ID == avatarID);
            if (avatarState == null)
            {
                return ErrorHandler.AvatarNotFoundError<FunctionUpdateAvatarIDResult>();
            }
            if (!avatarState.IsAvailable)
            {
                return ErrorHandler.AvatarNotAvailableError<FunctionUpdateAvatarIDResult>();
            }
            var saveResult = await BaseAzureModule.SaveProfileInternalDataAsync(profileID, ProfileDataKeys.AvatarID, avatarID);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateAvatarIDResult>(saveResult.Error);
            }
            var tableResult = await TableProfileAssistant.UpdateProfileAvatarIDAsync(profileID, avatarID);
            if (tableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionUpdateAvatarIDResult>(tableResult.Error);
            }
            foreach (var state in states)
            {
                state.Selected = false;
            }
            avatarState.Selected = true;
            return new ExecuteResult<FunctionUpdateAvatarIDResult>
            {
                Result = new FunctionUpdateAvatarIDResult
                {
                    SelectedAvatarID = avatarID,
                    UpdatedStates = avatarTableResult
                }
            };
        }

        public static async Task<ExecuteResult<AvatarsTable>> GetAvatarTableAsync()
        {
            var titleKey = TitleKeys.ProfileAvatarsTableKey;
            var result = await GetInternalTitleDataAsObjectAsync<AvatarsTable>(titleKey);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<AvatarsTable>(result.Error);
            }
            var table = result.Result;
            if (table == null)
            {
                return ErrorHandler.AvatarTableNotConfiguratedError<AvatarsTable>();
            }
            return new ExecuteResult<AvatarsTable>{
                Result = result.Result
            };
        }

        public static async Task<ExecuteResult<PlayFab.AdminModels.UserAccountInfo>> GetProfileAccountInfoByIdOrName(string profileID, string displayName)
        {
            var adminAPI = await GetFabAdminAPIAsync();
            var request = new PlayFab.AdminModels.LookupUserAccountInfoRequest
            {
                PlayFabId = profileID,
                TitleDisplayName = displayName
            };
            var userResult = await adminAPI.GetUserAccountInfoAsync(request);
            if (userResult.Error != null)
            {
                return ErrorHandler.ThrowError<PlayFab.AdminModels.UserAccountInfo>(userResult.Error);
            }
            var accInfo = userResult.Result.UserInfo;

            return new ExecuteResult<PlayFab.AdminModels.UserAccountInfo>
            {
                Result = accInfo
            };
        }

        public static async Task<ExecuteResult<AvatarsTableWithStates>> GetAvatarTableWithStatesAsync(string profileID)
        {
            var titleKey = TitleKeys.ProfileAvatarsTableKey;
            var result = await GetInternalTitleDataAsObjectAsync<AvatarsTable>(titleKey);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<AvatarsTableWithStates>(result.Error);
            }
            var table = result.Result;
            if (table == null)
            {
                return ErrorHandler.AvatarTableNotConfiguratedError<AvatarsTableWithStates>();
            }
            var avatars = table.Avatars ?? new List<CBSSpriteAvatar>();
            var hasAnyPrice = avatars.Any(x=>x.Purchasable);
            var hasAnyLevelLimit = avatars.Any(x=>x.HasLevelLimit);
            var avatarStates = avatars.Select(x=>x.ToState()).ToList();
            
            // reset to default state
            foreach (var state in avatarStates)
            {
                state.IsAvailable = true;
                state.Purchased = false;
                state.Selected = false;
            }
            // check selected
            var avatarResult = await GetProfileAvatarIDAsync(profileID);
            if (avatarResult.Error != null)
            {
                return ErrorHandler.ThrowError<AvatarsTableWithStates>(avatarResult.Error);
            }
            var selectedAvatarID = avatarResult.Result;
            if (!string.IsNullOrEmpty(selectedAvatarID))
            {
                var selectedState = avatarStates.FirstOrDefault(x=>x.ID == selectedAvatarID);
                if (selectedState != null)
                {
                    selectedState.Selected = true;
                }
            }
            // check level limit
            if (hasAnyLevelLimit)
            {
                var levelDetail = await ProfileExpModule.GetProfileExpirienceDetailAsync(profileID);
                if (levelDetail.Error != null)
                {
                    return ErrorHandler.ThrowError<AvatarsTableWithStates>(levelDetail.Error);
                }
                var level = levelDetail.Result.CurrentLevel;
                foreach (var state in avatarStates)
                {
                    var hasLimit = state.HasLevelLimit;
                    if (hasLimit)
                    {
                        state.LockedByLevel = level < state.LevelLimit;
                        state.IsAvailable = !state.LockedByLevel;
                    }
                }
            }
            // check purchase
            if (hasAnyPrice)
            {
                var purchasedResult = await GetPurchasedAvatarsAsync(profileID);
                if (purchasedResult.Error != null)
                {
                    return ErrorHandler.ThrowError<AvatarsTableWithStates>(purchasedResult.Error);
                }
                var resultObject = purchasedResult.Result;
                if (resultObject != null)
                {
                    var ids = resultObject.IDs ?? new List<string>();
                    foreach (var state in avatarStates)
                    {
                        var hasPrice = state.Purchasable;
                        if (hasPrice)
                        {
                            var purchased = ids.Contains(state.ID);
                            state.Purchased = purchased;
                            if (!state.LockedByLevel)
                            {
                                state.IsAvailable = purchased;
                            }
                        }
                    }
                }
            }
            // return final result
            return new ExecuteResult<AvatarsTableWithStates>
            {
                Result = new AvatarsTableWithStates
                {
                    AvatarStates = avatarStates
                }
            };
        }

        public static async Task<ExecuteResult<FunctionPurchaseAvatarResult>> PurchaseAvatarAsync(string profileID, string avatarID)
        {
            var statesResult = await GetAvatarTableWithStatesAsync(profileID);
            if (statesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPurchaseAvatarResult>(statesResult.Error);
            }
            var avatarTableResult = statesResult.Result;
            var states = avatarTableResult.AvatarStates;
            var avatarState = states.FirstOrDefault(x=>x.ID == avatarID);
            if (avatarState == null)
            {
                return ErrorHandler.AvatarNotFoundError<FunctionPurchaseAvatarResult>();
            }
            var hasPrice = avatarState.Purchasable;
            var avatarPrice = avatarState.Price;
            if (!hasPrice || avatarPrice == null || !avatarPrice.IsValid())
            {
                return ErrorHandler.AvatarHasNoPriceError<FunctionPurchaseAvatarResult>();
            }
            var currencyResult = await CurrencyModule.GetProfileCurrenciesAsync(profileID);
            if (currencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPurchaseAvatarResult>(currencyResult.Error);
            }
            var currency = currencyResult.Result.Currencies;
            var getAvatarsList = await GetPurchasedAvatarsAsync(profileID);
            if (getAvatarsList.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPurchaseAvatarResult>(getAvatarsList.Error);
            }
            var containerList = getAvatarsList.Result;
            var purchasedList = containerList.IDs;
            if (purchasedList.Contains(avatarID)) 
            {
                return ErrorHandler.AlreadyPurchasedError<FunctionPurchaseAvatarResult>();
            }
            var fabCurrencyResult = await CurrencyModule.GetProfileCurrenciesAsync(profileID);
            if (fabCurrencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPurchaseAvatarResult>(fabCurrencyResult.Error);
            }
            var fabCurrency = fabCurrencyResult.Result.Currencies;
            if (fabCurrency == null || !fabCurrency.ContainsKey(avatarPrice.CurrencyID))
            {
                return ErrorHandler.InsufficientFundsError<FunctionPurchaseAvatarResult>();
            }
            var targetCurrency = fabCurrency[avatarPrice.CurrencyID];
            if (targetCurrency.Value < avatarPrice.CurrencyValue)
            {
                return ErrorHandler.InsufficientFundsError<FunctionPurchaseAvatarResult>();
            }
            var subtractResult = await CurrencyModule.SubtractVirtualCurrencyFromProfileAsync(profileID, avatarPrice.CurrencyID, avatarPrice.CurrencyValue);
            if (subtractResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPurchaseAvatarResult>(subtractResult.Error);
            }
            purchasedList.Add(avatarID);
            var purchaseRawData = JsonConvert.SerializeObject(containerList);
            var saveResult = await SaveProfileInternalDataAsync(profileID, ProfileDataKeys.PurchasedAvatars, purchaseRawData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionPurchaseAvatarResult>(saveResult.Error);
            }
            avatarState.Purchased = true;
            if (!avatarState.LockedByLevel)
            {
                avatarState.IsAvailable = true;
            }
            return new ExecuteResult<FunctionPurchaseAvatarResult>
            {
                Result = new FunctionPurchaseAvatarResult
                {
                    PurchasedAvatarID = avatarID,
                    UpdatedStates = avatarTableResult,
                    AvatarPrice = avatarPrice
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGrantAvatarResult>> GrantAvatarAsync(string profileID, string avatarID)
        {
            var statesResult = await GetAvatarTableWithStatesAsync(profileID);
            if (statesResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantAvatarResult>(statesResult.Error);
            }
            var avatarTableResult = statesResult.Result;
            var states = avatarTableResult.AvatarStates;
            var avatarState = states.FirstOrDefault(x=>x.ID == avatarID);
            if (avatarState == null)
            {
                return ErrorHandler.AvatarNotFoundError<FunctionGrantAvatarResult>();
            }
            var hasPrice = avatarState.Purchasable;
            var avatarPrice = avatarState.Price;
            if (!hasPrice || avatarPrice == null || !avatarPrice.IsValid())
            {
                return ErrorHandler.AvatarHasNoPriceError<FunctionGrantAvatarResult>();
            }
            var currencyResult = await CurrencyModule.GetProfileCurrenciesAsync(profileID);
            if (currencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantAvatarResult>(currencyResult.Error);
            }
            var currency = currencyResult.Result.Currencies;
            var getAvatarsList = await GetPurchasedAvatarsAsync(profileID);
            if (getAvatarsList.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantAvatarResult>(getAvatarsList.Error);
            }
            var containerList = getAvatarsList.Result;
            var purchasedList = containerList.IDs;
            if (purchasedList.Contains(avatarID)) 
            {
                return ErrorHandler.AlreadyPurchasedError<FunctionGrantAvatarResult>();
            }

            purchasedList.Add(avatarID);
            var purchaseRawData = JsonConvert.SerializeObject(containerList);
            var saveResult = await SaveProfileInternalDataAsync(profileID, ProfileDataKeys.PurchasedAvatars, purchaseRawData);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantAvatarResult>(saveResult.Error);
            }
            avatarState.Purchased = true;
            if (!avatarState.LockedByLevel)
            {
                avatarState.IsAvailable = true;
            }
            return new ExecuteResult<FunctionGrantAvatarResult>
            {
                Result = new FunctionGrantAvatarResult
                {
                    GrantedAvatarID = avatarID,
                    UpdatedStates = avatarTableResult,
                }
            };
        }

        public static async Task<ExecuteResult<IDListContainer>> GetPurchasedAvatarsAsync(string profileID)
        {
            var dataResult = await BaseAzureModule.GetProfileInternalDataAsObject<IDListContainer>(profileID, ProfileDataKeys.PurchasedAvatars);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<IDListContainer>(dataResult.Error);
            }
            var dataList = dataResult.Result ?? new IDListContainer();
            dataList.IDs = dataList.IDs ?? new List<string>();
            return new ExecuteResult<IDListContainer>
            {
                Result = dataList
            };
        }

        public static async Task<ExecuteResult<string>> GetProfileClanIDAsync(string profileID)
        {
            var dataResult = await BaseAzureModule.GetProfileInternalRawData(profileID, ProfileDataKeys.ClanID);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<string>(dataResult.Error);
            }
            var clanID = dataResult.Result;
            return new ExecuteResult<string>
            {
                Result = clanID
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SetProfileClanIDAsync(string profileID, string clanID)
        {
            var setDataResult = await BaseAzureModule.SaveProfileInternalDataAsync(profileID, ProfileDataKeys.ClanID, clanID);
            if (setDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(setDataResult.Error);
            }

            var updateTableResult = await TableProfileAssistant.UpdateClanIDAsync(profileID, clanID);
            if (updateTableResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateTableResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<PlayFab.AdminModels.EntityKey>> GetProfileEntityKeyAsync(string profileID)
        {
            var getAccountResult = await GetProfileAccountInfoByIdOrName(profileID, null);
            if (getAccountResult.Error != null)
            {
                return ErrorHandler.ThrowError<PlayFab.AdminModels.EntityKey>(getAccountResult.Error);
            }
            var userInfo = getAccountResult.Result;
            var entityKey = userInfo.TitleInfo.TitlePlayerAccount;
            return new ExecuteResult<PlayFab.AdminModels.EntityKey>
            {
                Result = entityKey
            };
        }

        public static async Task<ExecuteResult<ProfileEntity>> GetProfileDetailByDisplayNameAsync(string displayName, CBSProfileConstraints constraints)
        {
            var getAccountResult = await GetProfileAccountInfoByIdOrName(null, displayName);
            if (getAccountResult.Error != null)
            {
                return ErrorHandler.ThrowError<ProfileEntity>(getAccountResult.Error);
            }
            var userInfo = getAccountResult.Result;
            var profileID = userInfo.PlayFabId;

            var profileResult = await TableProfileAssistant.GetProfileDetailAsync(profileID, constraints);
            if (profileResult.Error != null)
            {
                return ErrorHandler.ThrowError<ProfileEntity>(profileResult.Error);
            }
            var profileDetail = profileResult.Result;

            return new ExecuteResult<ProfileEntity>
            {
                Result = profileDetail
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> DeleteMasterPlayerAccountAsync(string profileID)
        {
            var adminAPI = await GetFabAdminAPIAsync();
            var request = new DeleteMasterPlayerAccountRequest
            {
                PlayFabId = profileID
            };
            var deleteResult = await adminAPI.DeleteMasterPlayerAccountAsync(request);
            if (deleteResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(deleteResult.Error);
            }

            await TableProfileAssistant.RemoveProfileEntryAsync(profileID);

            await TableNotificationAssistant.RemoveProfileEntryAsync(profileID);

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }
        
    }
}