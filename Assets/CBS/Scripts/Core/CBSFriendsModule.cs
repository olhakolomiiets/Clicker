using CBS.Models;
using CBS.Playfab;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSFriendsModule : CBSModule, IFriends
    {
        /// <summary>
        /// Notifies when a friend request has been approved.
        /// </summary>
        public event Action<string> OnFriendAccepted;
        /// <summary>
        /// Notifies when a friend request has been rejected.
        /// </summary>
        public event Action<string> OnFriendDeclined;
        /// <summary>
        /// Notifies when a new user has been added to your friends list.
        /// </summary>
        public event Action<string> OnFriendAdded;

        private IFabFriends FabFriends { get; set; }
        private IProfile Profile { get; set; }
        private ProfileConfigData ProfileConfig { get; set; }

        protected override void Init()
        {
            FabFriends = FabExecuter.Get<FabFriends>();
            Profile = Get<CBSProfileModule>();
            ProfileConfig = CBSScriptable.Get<ProfileConfigData>();
        }

        /// <summary>
        /// Get friends list of current profile
        /// </summary>
        /// <param name="result"></param>
        public void GetFriends(CBSProfileConstraints profileConstraints, Action<CBSGetFriendsResult> result)
        {
            var profileID = Profile.ProfileID;
            InternalGetProfileFriends(profileID, profileConstraints, result);
        }

        /// <summary>
        /// Get friends list of profile
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="profileConstraints"></param>
        /// <param name="result"></param>
        public void GetProfileFriends(string profileID, CBSProfileConstraints profileConstraints, Action<CBSGetFriendsResult> result)
        {
            InternalGetProfileFriends(profileID, profileConstraints, result);
        }

        /// <summary>
        /// Get a list of users who want to be friends with you.
        /// </summary>
        /// <param name="result"></param>
        public void GetRequestedFriends(CBSProfileConstraints profileConstraints, Action<CBSGetFriendsResult> result)
        {
            var profileID = Profile.ProfileID;
            var constraints = profileConstraints ?? GetDefaultProfileConstraints();
            FabFriends.GetRequestedFriendsList(profileID, constraints, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetFriendsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionFriendsResult>();
                    var profiles = functionResult.Profiles ?? new Dictionary<string, ProfileEntity>();
                    var maxValue = functionResult.Max;
                    var friendsList = profiles.Select(x => x.Value).ToList();
                    var currentCount = friendsList.Count;
                    result?.Invoke(new CBSGetFriendsResult
                    {
                        IsSuccess = true,
                        Friends = friendsList,
                        MaxValue = maxValue,
                        CurrentCount = currentCount,
                    });
                }
            }, onError =>
            {
                result?.Invoke(new CBSGetFriendsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Checks if a user is on your friends list.
        /// </summary>
        /// <param name="friendProfileID"></param>
        /// <param name="result"></param>
        public void CheckFriendship(string friendProfileID, Action<CBSCheckFriendshipResult> result)
        {
            var profileID = Profile.ProfileID;
            FabFriends.CheckFriendship(profileID, friendProfileID, onCheck =>
            {
                var cbsError = onCheck.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSCheckFriendshipResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onCheck.GetResult<FunctionCheckFriendshipResult>();
                    var friendID = functionResult.FriendProfileID;
                    var isFriend = functionResult.IsFriend;
                    var isRequestedFriend = functionResult.IsInRequestList;
                    var waitForAccept = functionResult.WaitForProfileAccept;
                    result?.Invoke(new CBSCheckFriendshipResult
                    {
                        IsSuccess = true,
                        FriendID = friendID,
                        ExistAsAcceptedFriend = isFriend,
                        ExistAsRequestedFriend = isRequestedFriend,
                        WaitForProfileAccept = waitForAccept
                    });
                }
            }, onError =>
            {
                result?.Invoke(new CBSCheckFriendshipResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Send friend request to profile.
        /// </summary>
        /// <param name="friendID"></param>
        /// <param name="result"></param>
        public void SendFriendsRequest(string friendID, Action<CBSSendFriendsRequestResult> result)
        {
            var profileID = Profile.ProfileID;
            FabFriends.SendFriendsRequest(profileID, friendID, onCheck =>
            {
                var cbsError = onCheck.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSSendFriendsRequestResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onCheck.GetResult<FunctionFriendRequestResult>();
                    var profileFriendID = functionResult.FriendProfileID;
                    result?.Invoke(new CBSSendFriendsRequestResult
                    {
                        IsSuccess = true,
                        FriendID = profileFriendID
                    });
                }
            }, onError =>
            {
                result?.Invoke(new CBSSendFriendsRequestResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Remove user from friends.
        /// </summary>
        /// <param name="friendProfileID"></param>
        /// <param name="result"></param>
        public void RemoveFriend(string friendProfileID, Action<CBSActionWithFriendResult> result)
        {
            var profileID = Profile.ProfileID;
            FabFriends.RemoveFriend(profileID, friendProfileID, onRemove =>
            {
                var cbsError = onRemove.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSActionWithFriendResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onRemove.GetResult<FunctionFriendRequestResult>();
                    var profileFriendID = functionResult.FriendProfileID;
                    result?.Invoke(new CBSActionWithFriendResult
                    {
                        IsSuccess = true,
                        FriendID = profileFriendID
                    });
                }
            }, onError =>
            {
                result?.Invoke(new CBSActionWithFriendResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Reject a user's friend request.
        /// </summary>
        /// <param name="friendProfileID"></param>
        /// <param name="result"></param>
        public void DeclineFriendRequest(string friendProfileID, Action<CBSActionWithFriendResult> result)
        {
            var profileID = Profile.ProfileID;
            FabFriends.DeclineFriend(profileID, friendProfileID, onDecline =>
            {
                var cbsError = onDecline.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSActionWithFriendResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onDecline.GetResult<FunctionFriendRequestResult>();
                    var profileFriendID = functionResult.FriendProfileID;
                    OnFriendDeclined?.Invoke(profileFriendID);
                    result?.Invoke(new CBSActionWithFriendResult
                    {
                        IsSuccess = true,
                        FriendID = profileFriendID
                    });
                }
            }, onError =>
            {
                result?.Invoke(new CBSActionWithFriendResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Approve the user's friend request.
        /// </summary>
        /// <param name="friendProfileID"></param>
        /// <param name="result"></param>
        public void AcceptFriend(string friendProfileID, Action<CBSActionWithFriendResult> result)
        {
            var profileID = Profile.ProfileID;
            FabFriends.AcceptFriend(profileID, friendProfileID, onAccept =>
            {
                var cbsError = onAccept.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSActionWithFriendResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onAccept.GetResult<FunctionFriendRequestResult>();
                    var profileFriendID = functionResult.FriendProfileID;
                    OnFriendAccepted?.Invoke(profileFriendID);
                    OnFriendAdded?.Invoke(profileFriendID);
                    result?.Invoke(new CBSActionWithFriendResult
                    {
                        IsSuccess = true,
                        FriendID = profileFriendID
                    });
                }
            }, onError =>
            {
                result?.Invoke(new CBSActionWithFriendResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Add user to friends without confirmation.
        /// </summary>
        /// <param name="friendProfileID"></param>
        /// <param name="result"></param>
        public void ForceAddFriend(string friendProfileID, Action<CBSActionWithFriendResult> result)
        {
            var profileID = Profile.ProfileID;
            FabFriends.ForceAddFriend(profileID, friendProfileID, onAdd =>
            {
                var cbsError = onAdd.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSActionWithFriendResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onAdd.GetResult<FunctionFriendRequestResult>();
                    var profileFriendID = functionResult.FriendProfileID;
                    OnFriendAdded?.Invoke(profileFriendID);
                    result?.Invoke(new CBSActionWithFriendResult
                    {
                        IsSuccess = true,
                        FriendID = profileFriendID
                    });
                }
            }, onError =>
            {
                result?.Invoke(new CBSActionWithFriendResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Get friends badge information. Return requested friends count.
        /// </summary>
        /// <param name="result"></param>
        public void GetFriendsBadge(Action<CBSBadgeResult> result)
        {
            var profileID = Profile.ProfileID;
            FabFriends.GetFriendsBadge(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionBadgeResult>();
                    var count = functionResult.Count;
                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = true,
                        Count = count
                    });
                }
            }, onError =>
            {
                result?.Invoke(new CBSBadgeResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Get shared friends list with profile
        /// </summary>
        /// <param name="withProfileID"></param>
        /// <param name="profileConstraints"></param>
        /// <param name="result"></param>
        public void GetSharedFriendsWithProfile(string withProfileID, CBSProfileConstraints profileConstraints, Action<CBSGetFriendsResult> result)
        {
            var profileID = Profile.ProfileID;
            var constraints = profileConstraints ?? GetDefaultProfileConstraints();
            FabFriends.GetSharedFriends(profileID, withProfileID, constraints, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetFriendsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionFriendsResult>();
                    var profiles = functionResult.Profiles ?? new Dictionary<string, ProfileEntity>();
                    var friendsList = profiles.Select(x => x.Value).ToList();
                    var currentCount = friendsList.Count;
                    result?.Invoke(new CBSGetFriendsResult
                    {
                        IsSuccess = true,
                        Friends = friendsList,
                        CurrentCount = currentCount,
                    });
                }
            }, onError =>
            {
                result?.Invoke(new CBSGetFriendsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        // internal
        public void InternalGetProfileFriends(string profileID, CBSProfileConstraints profileConstraints, Action<CBSGetFriendsResult> result)
        {
            var constraints = profileConstraints ?? GetDefaultProfileConstraints();
            FabFriends.GetFriendsList(profileID, constraints, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetFriendsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionFriendsResult>();
                    var profiles = functionResult.Profiles ?? new Dictionary<string, ProfileEntity>();
                    var maxValue = functionResult.Max;
                    var friendsList = profiles.Select(x => x.Value).ToList();
                    var currentCount = friendsList.Count;
                    result?.Invoke(new CBSGetFriendsResult
                    {
                        IsSuccess = true,
                        Friends = friendsList,
                        MaxValue = maxValue,
                        CurrentCount = currentCount,
                    });
                }
            }, onError =>
            {
                result?.Invoke(new CBSGetFriendsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        private CBSProfileConstraints GetDefaultProfileConstraints()
        {
            var profileConfig = ProfileConfig.EnableOnlineStatus;
            return new CBSProfileConstraints
            {
                LoadAvatar = true,
                LoadOnlineStatus = profileConfig
            };
        }
    }
}
