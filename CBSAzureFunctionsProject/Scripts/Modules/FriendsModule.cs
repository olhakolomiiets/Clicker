using PlayFab.ServerModels;
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

namespace CBS
{
    public class FriendsModule : BaseAzureModule
    {
        private static readonly string  FriendsAcceptTag = "Accept";
        private static readonly string  FriendsRequestTag = "Request";

        [FunctionName(AzureFunctions.GetFriendsListMethod)]
        public static async Task<dynamic> GetFriendsListTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetFriendsListRequest>();

            var profileID = request.ProfileID;
            var constraints = request.Constraints;

            var getResult = await GetAcceptedFriendsListAsync(profileID, constraints);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetRequestedFriendsListMethod)]
        public static async Task<dynamic> GetRequestedFriendsListTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetFriendsListRequest>();

            var profileID = request.ProfileID;
            var constraints = request.Constraints;

            var getResult = await GetRequestedFriendsListAsync(profileID, constraints);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SendFriendRequestMethod)]
        public static async Task<dynamic> SendFriendRequestTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionFriendsRequest>();

            var profileID = request.ProfileID;
            var friendID = request.FriendProfileID;

            var sendResult = await SendFriendsRequestAsync(profileID, friendID);
            if (sendResult.Error != null)
            {
                return ErrorHandler.ThrowError(sendResult.Error).AsFunctionResult();
            }

            return sendResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.CheckFriendshipMethod)]
        public static async Task<dynamic> CheckFriendshipTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionFriendsRequest>();

            var profileID = request.ProfileID;
            var friendID = request.FriendProfileID;

            var checkResult = await CheckFriendShipAsync(profileID, friendID);
            if (checkResult.Error != null)
            {
                return ErrorHandler.ThrowError(checkResult.Error).AsFunctionResult();
            }

            return checkResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AcceptFriendRequestMethod)]
        public static async Task<dynamic> AcceptFriendTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionFriendsRequest>();

            var profileID = request.ProfileID;
            var friendID = request.FriendProfileID;

            var acceptResult = await AcceptFriendRequestAsync(profileID, friendID);
            if (acceptResult.Error != null)
            {
                return ErrorHandler.ThrowError(acceptResult.Error).AsFunctionResult();
            }

            return acceptResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.DeclineFriendRequestMethod)]
        public static async Task<dynamic> DeclineFriendRequestTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionFriendsRequest>();

            var profileID = request.ProfileID;
            var friendID = request.FriendProfileID;

            var declineResult = await DeclineFriendRequestAsync(profileID, friendID);
            if (declineResult.Error != null)
            {
                return ErrorHandler.ThrowError(declineResult.Error).AsFunctionResult();
            }

            return declineResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.RemoveFriendMethod)]
        public static async Task<dynamic> RemoveFriendTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionFriendsRequest>();

            var profileID = request.ProfileID;
            var friendID = request.FriendProfileID;

            var removeResult = await RemoveFriendAsync(profileID, friendID);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError(removeResult.Error).AsFunctionResult();
            }

            return removeResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetFriendsBadgeMethod)]
        public static async Task<dynamic> GetFriendsBadgeTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var profileID = request.ProfileID;

            var getResult = await GetFriendsBadgeAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetSharedFriendsMethod)]
        public static async Task<dynamic> GetSharedFriendsTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGetSharedFriendsRequest>();

            var profileID = request.ProfileID;
            var withProfileID = request.WithProfileID;
            var constraints = request.Constraints;

            var getResult = await GetSharedFriendsListAsync(profileID, withProfileID, constraints);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<List<FriendInfo>>> GetFriendsListAsync(string profileID)
        {
            var friendsRequest = new GetFriendsListRequest {
                PlayFabId = profileID,
                ProfileConstraints = new PlayerProfileViewConstraints {
                    ShowTags = true
                }
            };
            var friendResult = await FabServerAPI.GetFriendsListAsync(friendsRequest);
            if (friendResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<FriendInfo>>(friendResult.Error);
            }
            var friends = friendResult.Result.Friends ?? new List<FriendInfo>();
            return new ExecuteResult<List<FriendInfo>>
            {
                Result = friends
            };
        }

        public static async Task<ExecuteResult<FunctionFriendsResult>> GetAcceptedFriendsListAsync(string profileID, CBSProfileConstraints constraints)
        {
            var getMetaResult = await GetFriendsMetaAsync();
            if (getMetaResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendsResult>(getMetaResult.Error);
            }
            var metaData = getMetaResult.Result;
            var maxValue = metaData.MaxFriend;

            var friendsResult = await GetFriendsListAsync(profileID);
            if (friendsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendsResult>(friendsResult.Error);
            }
            var friendsList = friendsResult.Result;
            var acceptedFriends = friendsList.Where(x=>x.GetTag() == FriendsAcceptTag);
            var acceptedFriendsIDs = acceptedFriends.Select(x=>x.FriendPlayFabId).ToArray();
            var profileDetailsResult = await TableProfileAssistant.GetProfilesDetailsAsync(acceptedFriendsIDs, constraints);
            if (profileDetailsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendsResult>(profileDetailsResult.Error);
            }
            var profiles = profileDetailsResult.Result;
            var friendsProfiles = new Dictionary<string, ProfileEntity>();
            foreach (var id in acceptedFriendsIDs)
            {
                friendsProfiles[id] = profiles.ContainsKey(id) ? profiles[id] : new ProfileEntity
                {
                    ProfileID = id 
                };
            }
            return new ExecuteResult<FunctionFriendsResult>
            {
                Result = new FunctionFriendsResult
                {
                    Profiles = friendsProfiles,
                    Max = maxValue
                }
            };
        }

        public static async Task<ExecuteResult<FunctionFriendsResult>> GetRequestedFriendsListAsync(string profileID, CBSProfileConstraints constraints)
        {
            var getMetaResult = await GetFriendsMetaAsync();
            if (getMetaResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendsResult>(getMetaResult.Error);
            }
            var metaData = getMetaResult.Result;
            var maxValue = metaData.MaxRequested;

            var friendsResult = await GetFriendsListAsync(profileID);
            if (friendsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendsResult>(friendsResult.Error);
            }
            var friendsList = friendsResult.Result;
            var acceptedFriends = friendsList.Where(x=>x.GetTag() == FriendsRequestTag);
            var acceptedFriendsIDs = acceptedFriends.Select(x=>x.FriendPlayFabId).ToArray();
            var profileDetailsResult = await TableProfileAssistant.GetProfilesDetailsAsync(acceptedFriendsIDs, constraints);
            if (profileDetailsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendsResult>(profileDetailsResult.Error);
            }
            var profiles = profileDetailsResult.Result;
            var friendsProfiles = new Dictionary<string, ProfileEntity>();
            foreach (var id in acceptedFriendsIDs)
            {
                friendsProfiles[id] = profiles.ContainsKey(id) ? profiles[id] : new ProfileEntity
                {
                    ProfileID = id 
                };
            }
            return new ExecuteResult<FunctionFriendsResult>
            {
                Result = new FunctionFriendsResult
                {
                    Profiles = friendsProfiles,
                    Max = maxValue
                }
            };
        }

        public static async Task<ExecuteResult<FunctionFriendRequestResult>> SendFriendsRequestAsync(string profileID, string friendID)
        {
            // check limitation
            var getFriendsResult = await GetFriendsListAsync(friendID);
            if (getFriendsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(getFriendsResult.Error);
            }
            var friendsList = getFriendsResult.Result ?? new List<FriendInfo>();
            var requestedCount = friendsList.Where(x=>x.GetTag() == FriendsRequestTag).Count();

            var getMetaResult = await GetFriendsMetaAsync();
            if (getMetaResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(getMetaResult.Error);
            }
            var metaData = getMetaResult.Result;
            var maxRequested = metaData.MaxRequested;
            if (requestedCount > maxRequested)
            {
                return ErrorHandler.FriendsLimitReached<FunctionFriendRequestResult>();
            }

            var friendsRequest = new AddFriendRequest {
                PlayFabId = profileID,
                FriendPlayFabId = friendID
            };
            var addResult = await FabServerAPI.AddFriendAsync(friendsRequest);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(addResult.Error);
            }

            friendsRequest.PlayFabId = friendID;
            friendsRequest.FriendPlayFabId = profileID;
            
            addResult = await FabServerAPI.AddFriendAsync(friendsRequest);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(addResult.Error);
            }

            var setTagRequest = new SetFriendTagsRequest {
                PlayFabId = friendID,
                FriendPlayFabId = profileID,
                Tags = new List<string>() {FriendsRequestTag}
            };
            
            var tagResult = await FabServerAPI.SetFriendTagsAsync(setTagRequest);
            if (tagResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(tagResult.Error);
            }

            return new ExecuteResult<FunctionFriendRequestResult>
            {
                Result = new FunctionFriendRequestResult
                {
                    FriendProfileID = friendID
                }
            };
        }

        public static async Task<ExecuteResult<FunctionCheckFriendshipResult>> CheckFriendShipAsync(string profileID, string friendID)
        {
            var getProfileFriendsResult = await GetFriendsListAsync(friendID);
            if (getProfileFriendsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCheckFriendshipResult>(getProfileFriendsResult.Error);
            }
            var profileFriendList = getProfileFriendsResult.Result ?? new List<FriendInfo>();
            var profileRequestedList = profileFriendList.Where(x=>x.GetTag() == FriendsRequestTag);
            var waitForProfileAccept = profileRequestedList.Any(x=>x.FriendPlayFabId == profileID);

            var friendListResult = await GetFriendsListAsync(profileID);
            if (friendListResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCheckFriendshipResult>(friendListResult.Error);
            }
            var friendList = friendListResult.Result;
            var profileExist = friendList.Any(x=>x.FriendPlayFabId == friendID);
            if (profileExist)
            {
                var friendInfo = friendList.FirstOrDefault(x=>x.FriendPlayFabId == friendID);
                var friendTag = friendInfo.GetTag();
                var accepted = friendTag == FriendsAcceptTag;
                var requested = friendTag == FriendsRequestTag;

                return new ExecuteResult<FunctionCheckFriendshipResult>
                {
                    Result = new FunctionCheckFriendshipResult
                    {
                        FriendProfileID = friendID,
                        IsFriend = accepted,
                        IsInRequestList = requested,
                        WaitForProfileAccept = waitForProfileAccept
                    }
                };
            }
            else
            {
                return new ExecuteResult<FunctionCheckFriendshipResult>
                {
                    Result = new FunctionCheckFriendshipResult
                    {
                        FriendProfileID = friendID
                    }
                };
            }
        }

        public static async Task<ExecuteResult<FunctionFriendRequestResult>> RemoveFriendAsync(string profileID, string friendID)
        {
            var request = new RemoveFriendRequest {
                PlayFabId = profileID,
                FriendPlayFabId = friendID
            };            
            var removeResult = await FabServerAPI.RemoveFriendAsync(request);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(removeResult.Error);
            }
            
            request.PlayFabId = friendID;
            request.FriendPlayFabId = profileID;
            removeResult = await FabServerAPI.RemoveFriendAsync(request);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(removeResult.Error);
            }

            return new ExecuteResult<FunctionFriendRequestResult>
            {
                Result = new FunctionFriendRequestResult
                {
                    FriendProfileID = friendID
                }
            };
        }

        public static async Task<ExecuteResult<FunctionFriendRequestResult>> DeclineFriendRequestAsync(string profileID, string friendID)
        {
            var removeResult = await RemoveFriendAsync(profileID, friendID);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(removeResult.Error);
            }

            return new ExecuteResult<FunctionFriendRequestResult>
            {
                Result = new FunctionFriendRequestResult
                {
                    FriendProfileID = friendID
                }
            };
        }

        public static async Task<ExecuteResult<FunctionFriendRequestResult>> AcceptFriendRequestAsync(string profileID, string friendID)
        {
            // check limitation
            var getFriendsResult = await GetFriendsListAsync(profileID);
            if (getFriendsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(getFriendsResult.Error);
            }
            var friendsList = getFriendsResult.Result ?? new List<FriendInfo>();
            var friendsCount = friendsList.Where(x=>x.GetTag() == FriendsAcceptTag).Count();

            var getMetaResult = await GetFriendsMetaAsync();
            if (getMetaResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(getMetaResult.Error);
            }
            var metaData = getMetaResult.Result;
            var maxFriendsCount = metaData.MaxFriend;
            if (friendsCount >= maxFriendsCount)
            {
                return ErrorHandler.FriendsLimitReached<FunctionFriendRequestResult>();
            }

            var setTagRequset = new SetFriendTagsRequest {
                PlayFabId = friendID,
                FriendPlayFabId = profileID,
                Tags = new List<string> {FriendsAcceptTag}
            };         
            var tagResult = await FabServerAPI.SetFriendTagsAsync(setTagRequset);
            if (tagResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(tagResult.Error);
            }
            
            setTagRequset.PlayFabId = profileID;
            setTagRequset.FriendPlayFabId = friendID;        
            tagResult = await FabServerAPI.SetFriendTagsAsync(setTagRequset);
            if (tagResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(tagResult.Error);
            }

            return new ExecuteResult<FunctionFriendRequestResult>
            {
                Result = new FunctionFriendRequestResult
                {
                    FriendProfileID = friendID
                }
            };
        }

        public static async Task<ExecuteResult<FunctionFriendRequestResult>> ForceAddToFriendAsync(string profileID, string friendID)
        {
            var acceptFriendResult = await AcceptFriendRequestAsync(profileID, friendID);
            if (acceptFriendResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionFriendRequestResult>(acceptFriendResult.Error);
            }
            return new ExecuteResult<FunctionFriendRequestResult>
            {
                Result = new FunctionFriendRequestResult
                {
                    FriendProfileID = friendID
                }
            };
        }

        public static async Task<ExecuteResult<FunctionFriendsResult>> GetSharedFriendsListAsync(string profileID, string withProfileID, CBSProfileConstraints constraints)
        {
            var getFriendsTask = GetAcceptedFriendsListAsync(profileID, constraints);
            var getProfileFriendsTask = GetAcceptedFriendsListAsync(withProfileID, constraints);
            var taskList = new List<Task<ExecuteResult<FunctionFriendsResult>>>();
            taskList.Add(getFriendsTask);
            taskList.Add(getProfileFriendsTask);
            var tasksResults = await Task.WhenAll(taskList);
            var hasError = tasksResults.Any(x=>x.Error != null);
            if (hasError)
            {
                return ErrorHandler.ThrowError<FunctionFriendsResult>(tasksResults.FirstOrDefault(x=>x.Error != null).Error);
            }
            var friendList = tasksResults[0].Result.Profiles ?? new Dictionary<string, ProfileEntity>();
            var profileFriendList = tasksResults[1].Result.Profiles ?? new Dictionary<string, ProfileEntity>();
            var sharedList = friendList.Where(a => profileFriendList.Any(b => a.Value.ProfileID == b.Value.ProfileID)).ToDictionary(x=>x.Key, x=> x.Value);

            return new ExecuteResult<FunctionFriendsResult>
            {
                Result = new FunctionFriendsResult
                {
                    Profiles = sharedList
                }
            };
        }

        public static async Task<ExecuteResult<FunctionBadgeResult>> GetFriendsBadgeAsync(string profileID)
        {
            var friendsResult = await GetFriendsListAsync(profileID);
            if (friendsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionBadgeResult>(friendsResult.Error);
            }
            var friendsList = friendsResult.Result ?? new List<FriendInfo>();
            var requestedFriends = friendsList.Where(x=>x.GetTag() == FriendsRequestTag);
            var requestedCount = requestedFriends.Count();

            return new ExecuteResult<FunctionBadgeResult>
            {
                Result = new FunctionBadgeResult
                {
                    Count = requestedCount
                }
            };
        }

        public static async Task<ExecuteResult<FriendsMetaData>> GetFriendsMetaAsync()
        {
            var getDataResult = await GetInternalTitleDataAsObjectAsync<FriendsMetaData>(TitleKeys.FriendsDataKey);
            if (getDataResult.Error != null)
            {
                return ErrorHandler.ThrowError<FriendsMetaData>(getDataResult.Error);
            }
            var metaData = getDataResult.Result ?? FriendsMetaData.Default();
            return new ExecuteResult<FriendsMetaData>
            {
                Result = metaData
            };
        }
    }
}