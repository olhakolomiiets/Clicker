using CBS.Core.Auth;
using CBS.Models;
using CBS.Playfab;
using CBS.Scriptable;
using CBS.Utils;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSProfileModule : CBSModule, IProfile
    {
        /// <summary>
        /// An event that reports when the username has been updated.
        /// </summary>
        public event Action<CBSUpdateDisplayNameResult> OnDisplayNameUpdated;
        /// <summary>
        /// An event that reports when the profile avatar has been updated.
        /// </summary>
        public event Action<AvatarInfo> OnAvatarUpdated;
        /// <summary>
        /// An event that reports when information about the current user has been received.
        /// </summary>
        public event Action<CBSGetAccountInfoResult> OnAcountInfoGetted;
        /// <summary>
        /// An event that reports when the current player's experience points have been changed.
        /// </summary>
        public event Action<CBSLevelDataResult> OnPlayerExperienceUpdated;
        /// <summary>
        /// An event that reports when the linked acoounts state have been changed.
        /// </summary>
        public event Action<AccountLinkedInfo> OnLinkedAccountInfoChanged;

        private IAuth Auth { get; set; }

        private IFabProfile FabProfile { get; set; }

        private OnlineStatusProcessor OnlineProcessor { get; set; }
        private int OnlineThreshold { get; set; }

        private AuthData AuthData { get; set; }
        private ProfileConfigData ProfileData { get; set; }

        // profile public data

        /// <summary>
        /// Unique user identifier.
        /// </summary>
        public string ProfileID { get; private set; }

        /// <summary>
        /// Display name of current user.
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// Profile clna ID is exsit
        /// </summary>
        public string ClanID { get; private set; }

        /// <summary>
        /// Registration date of the current user.
        /// </summary>
        public DateTime RegistrationDate { get; private set; }

        /// <summary>
        /// Entity ID of current user. Used by Playfab for new features such as groups for example.
        /// </summary>
        public string EntityID { get; private set; }

        /// <summary>
        /// Entity type of current user. For profile its always "title_player_account"
        /// </summary>
        public string EntityType { get; private set; }

        /// <summary>
        /// Cached Entity key
        /// </summary>
        public EntityKey EntityKey => new EntityKey { Id = EntityID, Type = EntityType };

        /// <summary>
        /// Last cached user level information
        /// </summary>
        public LevelInfo CachedLevelInfo { get; private set; } = new LevelInfo();

        /// <summary>
        /// Information about linked accounts
        /// </summary>
        public AccountLinkedInfo LinkedAccounts { get; private set; } = new AccountLinkedInfo();


        /// <summary>
        /// Last cached profile data
        /// </summary>
        public Dictionary<string, UserDataRecord> CachedProfileData { get; private set; }

        /// <summary>
        /// Infrormation about player avatar id or image url.
        /// </summary>
        public AvatarInfo Avatar { get; private set; }

        /// <summary>
        /// Check if profile exist if clan
        /// </summary>
        public bool ExistInClan { get; private set; }

        /// <summary>
        /// Clan entity of joined clan
        /// </summary>
        public ClanEntity ClanEntity { get; private set; }

        /// <summary>
        /// Profile session context
        /// </summary>
        public PlayFabAuthenticationContext AuthenticationContex { get; private set; }


        protected override void Init()
        {
            AuthData = CBSScriptable.Get<AuthData>();
            ProfileData = CBSScriptable.Get<ProfileConfigData>();
            Auth = Get<CBSAuthModule>();
            FabProfile = FabExecuter.Get<FabProfile>();

            OnlineThreshold = ProfileData.ConsiderInactiveAfter;
            Auth.OnLoginEvent += OnProfileLoggedIn;
        }

        // API calls

        /// <summary>
        /// Update player display name
        /// </summary>
        /// <param name="displayName"></param>
        /// <param name="result"></param>
        public void UpdateDisplayName(string displayName, Action<CBSUpdateDisplayNameResult> result = null)
        {
            var profanityCheck = AuthData.DisplayNameProfanityCheck;
            FabProfile.UpdateUserDisplayName(ProfileID, displayName, profanityCheck, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    CBSUpdateDisplayNameResult callback = new CBSUpdateDisplayNameResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    };
                    result?.Invoke(callback);
                }
                else
                {
                    DisplayName = displayName;
                    CBSUpdateDisplayNameResult callback = new CBSUpdateDisplayNameResult
                    {
                        IsSuccess = true,
                        DisplayName = DisplayName
                    };

                    result?.Invoke(callback);
                    OnDisplayNameUpdated?.Invoke(callback);
                }
            }, onFailed =>
            {
                CBSUpdateDisplayNameResult callback = new CBSUpdateDisplayNameResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                };
                result?.Invoke(callback);
            });
        }

        /// <summary>
        /// Get full information of current player account. Include all Playfab origin result.
        /// </summary>
        /// <param name="result"></param>
        public void GetAccountInfo(Action<CBSGetAccountInfoResult> result)
        {
            FabProfile.GetUserAccountInfo(ProfileID, onSuccess =>
            {
                ParseAccountInfo(onSuccess.AccountInfo);

                var callbackData = new CBSGetAccountInfoResult
                {
                    IsSuccess = true,
                    PlayFabResult = onSuccess.AccountInfo,
                    DisplayName = DisplayName,
                    AvatarUrl = onSuccess.AccountInfo.TitleInfo.AvatarUrl
                };

                OnAcountInfoGetted?.Invoke(callbackData);
                result?.Invoke(callbackData);
            }, onError =>
            {
                var failedResult = new CBSGetAccountInfoResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };

                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Get full information of player account by id. Include all Playfab origin result.
        /// </summary>
        /// <param name="result"></param>
        public void GetProfileAccountInfo(string profileID, Action<CBSGetAccountInfoResult> result)
        {
            FabProfile.GetUserAccountInfo(profileID, onSuccess =>
            {
                string nickname = onSuccess.AccountInfo.TitleInfo.DisplayName;
                var callbackData = new CBSGetAccountInfoResult
                {
                    IsSuccess = true,
                    PlayFabResult = onSuccess.AccountInfo,
                    DisplayName = nickname
                };
                result?.Invoke(callbackData);
            }, onError =>
            {
                var failedResult = new CBSGetAccountInfoResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };

                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Adds N points of experience to the current state. In the response, you can get information whether the player has reached a new level and also information about the reward about the new level.
        /// </summary>
        /// <param name="expToAdd"></param>
        /// <param name="result"></param>
        public void AddExpirienceToProfile(int expToAdd, Action<CBSLevelDataResult> result = null)
        {
            FabProfile.AddPlayerExpirience(ProfileID, expToAdd, onSuccess =>
            {
                var cbsError = onSuccess.GetCBSError();
                if (cbsError == null)
                {
                    var rawData = onSuccess.FunctionResult.ToString();
                    var functionResult = onSuccess.GetResult<FunctionAddExpirienceResult>();
                    var levelData = ParseLevelData(rawData);
                    var isNewLevel = functionResult.NewLevelReached;
                    var rewardResult = functionResult.NewLevelReward;

                    var callbackData = new CBSLevelDataResult
                    {
                        IsSuccess = true,
                        LevelInfo = levelData,
                        IsNewLevel = isNewLevel,
                        NewLevelReward = rewardResult
                    };

                    if (isNewLevel && rewardResult != null)
                    {
                        var grantedCurrencies = rewardResult.GrantedCurrencies;
                        if (grantedCurrencies != null && grantedCurrencies.Count > 0)
                        {
                            foreach (var cc in grantedCurrencies)
                            {
                                Get<CBSCurrencyModule>().ChangeRequest(cc.Key);
                            }
                        }

                        var grantedInstances = rewardResult.GrantedInstances;
                        if (grantedInstances != null && grantedInstances.Count > 0)
                        {
                            var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                            Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        }
                    }

                    OnPlayerExperienceUpdated?.Invoke(callbackData);
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
        /// Get information about current experience/level of current profile
        /// </summary>
        /// <param name="result"></param>
        public void GetProfileLevelDetail(Action<CBSLevelDataResult> result)
        {
            GetProfileLevelDetail(ProfileID, result);
        }

        /// <summary>
        /// Get information about current experience/level of profile
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        public void GetProfileLevelDetail(string profileID, Action<CBSLevelDataResult> result)
        {
            FabProfile.GetProfileExpirienceDetail(profileID, onAdd =>
            {
                var cbsError = onAdd.GetCBSError();
                if (cbsError == null)
                {
                    var resultObject = onAdd.FunctionResult;
                    var rawData = resultObject == null ? JsonPlugin.EMPTY_JSON : resultObject.ToString();
                    var updateCache = profileID == ProfileID;
                    var levelData = ParseLevelData(rawData, updateCache);

                    var callbackData = new CBSLevelDataResult
                    {
                        IsSuccess = true,
                        LevelInfo = levelData
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
        /// Get an array with information about all profile levels in the game.
        /// </summary>
        /// <param name="result"></param>
        public void GetLevelTable(Action<CBSGetLevelTableResult> result)
        {
            FabProfile.GetLevelTable(ProfileID, onSuccess =>
            {
                var cbsError = onSuccess.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetLevelTableResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                var table = onSuccess.GetResult<LevelTable>();

                result?.Invoke(new CBSGetLevelTableResult
                {
                    IsSuccess = true,
                    Table = table
                });
            }, onError =>
            {
                result?.Invoke(new CBSGetLevelTableResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Get general game information about a player, including profile ID, avatar url, display name, player level and clan information.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void GetProfileDetail(CBSGetProfileRequest request, Action<CBSGetProfileResult> result)
        {
            FabProfile.GetProfileDetail(request.ProfileID, request.Constraints, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetProfileResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var tableProfile = onGet.GetResult<ProfileEntity>();

                    result?.Invoke(new CBSGetProfileResult
                    {
                        IsSuccess = true,
                        DisplayName = tableProfile.DisplayName,
                        ProfileID = tableProfile.ProfileID,
                        Avatar = tableProfile.Avatar,
                        ClanID = tableProfile.ClanID,
                        Level = tableProfile.Level,
                        Statistics = tableProfile.Statistics,
                        ProfileData = tableProfile.ProfileData,
                        OnlineStatus = tableProfile.OnlineStatus,
                        ClanEntity = tableProfile.ClanEntity
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetProfileResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get general game information about a player by display name, including profile ID, avatar url, display name, player level and clan information.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void GetProfileDetailByDisplayName(string displayName, CBSProfileConstraints constraints, Action<CBSGetProfileResult> result)
        {
            FabProfile.GetProfileDetailByDisplayName(displayName, constraints, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetProfileResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var tableProfile = onGet.GetResult<ProfileEntity>();

                    result?.Invoke(new CBSGetProfileResult
                    {
                        IsSuccess = true,
                        DisplayName = tableProfile.DisplayName,
                        ProfileID = tableProfile.ProfileID,
                        Avatar = tableProfile.Avatar,
                        ClanID = tableProfile.ClanID,
                        Level = tableProfile.Level,
                        Statistics = tableProfile.Statistics,
                        ProfileData = tableProfile.ProfileData,
                        OnlineStatus = tableProfile.OnlineStatus,
                        ClanEntity = tableProfile.ClanEntity
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetProfileResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get general game information about group of players, including profile ID, avatar url or id, display name, player level and clan information.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void GetProfilesDetails(CBSGetProfilesRequest request, Action<CBSGetProfilesResult> result)
        {
            FabProfile.GetProfilesDetails(request.ProfilesIDs, request.Constraints, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetProfilesResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var profilesResult = onGet.GetResult<FunctionProfilesResult>();

                    result?.Invoke(new CBSGetProfilesResult
                    {
                        IsSuccess = true,
                        Profiles = profilesResult.Profiles
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetProfilesResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get the specific player data by unique key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="result"></param>
        public void GetProfileData(string key, Action<CBSGetProfileDataResult> result)
        {
            InternalGetProfileData(ProfileID, new string[] { key }, result);
        }

        /// <summary>
        /// Get the specific player data by unique keys.
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="result"></param>
        public void GetProfileData(string[] keys, Action<CBSGetProfileDataResult> result)
        {
            InternalGetProfileData(ProfileID, keys, result);
        }

        /// <summary>
        /// Get the custom player data by profile id
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="key"></param>
        /// <param name="result"></param>
        public void GetProfileDataByPlayerID(string playerID, string key, Action<CBSGetProfileDataResult> result)
        {
            InternalGetProfileData(playerID, new string[] { key }, result);
        }

        /// <summary>
        /// Get the custom player data by profile id
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="keys"></param>
        /// <param name="result"></param>
        public void GetProfileDataByPlayerID(string playerID, string[] keys, Action<CBSGetProfileDataResult> result)
        {
            InternalGetProfileData(playerID, keys, result);
        }

        /// <summary>
        /// Set/Save custom player data of current profile. 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        public void SaveProfileData(string key, string value, Action<CBSBaseResult> result)
        {
            FabProfile.SetProfileData(ProfileID, key, value, onSave =>
            {
                var cbsError = onSave.GetCBSError();
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
        /// Set/Save custom player data set of current profile. 
        /// </summary>
        /// <param name="dataSet"></param>
        /// <param name="result"></param>
        public void SaveProfileData(Dictionary<string, string> dataSet, Action<CBSBaseResult> result)
        {
            FabProfile.SetProfileData(ProfileID, dataSet, onSave =>
            {
                var cbsError = onSave.GetCBSError();
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
        /// Update the current player's profile photo.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="result"></param>
        public void UpdateAvatarUrl(string url, Action<CBSUpdateAvatarUrlResult> result)
        {
            FabProfile.SetAvatarUrl(ProfileID, url, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSUpdateAvatarUrlResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    ParseAvatarUrl(url);
                    var updateResult = new CBSUpdateAvatarUrlResult
                    {
                        IsSuccess = true,
                        Url = url
                    };
                    result?.Invoke(updateResult);
                    OnAvatarUpdated?.Invoke(Avatar);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSUpdateAvatarUrlResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Update the current player avatar id.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="result"></param>
        public void UpdateAvatarID(string avatarID, Action<CBSUpdateAvatarIDResult> result)
        {
            FabProfile.UpdateProfileAvatarID(ProfileID, avatarID, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSUpdateAvatarIDResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    ParseAvatarID(avatarID);
                    var functionResult = onUpdate.GetResult<FunctionUpdateAvatarIDResult>();
                    var updatedStates = functionResult.UpdatedStates;
                    var updatedAvatarID = functionResult.SelectedAvatarID;
                    var updateResult = new CBSUpdateAvatarIDResult
                    {
                        IsSuccess = true,
                        AvatarID = avatarID,
                        UpdatedStates = updatedStates
                    };
                    result?.Invoke(updateResult);
                    OnAvatarUpdated?.Invoke(Avatar);
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSUpdateAvatarIDResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Grant an avatar if it has a price.
        /// </summary>
        /// <param name="avatarID"></param>
        /// <param name="result"></param>
        public void GrantAvatar(string avatarID, Action<CBSGrantAvatarResult> result)
        {
            FabProfile.GrantAvatar(ProfileID, avatarID, onPurchase =>
            {
                var cbsError = onPurchase.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGrantAvatarResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onPurchase.GetResult<FunctionGrantAvatarResult>();
                    result?.Invoke(new CBSGrantAvatarResult
                    {
                        IsSuccess = true,
                        GrantedAvatarID = functionResult.GrantedAvatarID,
                        UpdatedStates = functionResult.UpdatedStates
                    });
                }
            },
            onFailed =>
            {
                result?.Invoke(new CBSGrantAvatarResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Purchase an avatar if it has a price.
        /// </summary>
        /// <param name="avatarID"></param>
        /// <param name="result"></param>
        public void PurchaseAvatar(string avatarID, Action<CBSPurchaseAvatarResult> result)
        {
            FabProfile.PurchaseAvatar(ProfileID, avatarID, onPurchase =>
            {
                var cbsError = onPurchase.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSPurchaseAvatarResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onPurchase.GetResult<FunctionPurchaseAvatarResult>();
                    var price = functionResult.AvatarPrice;
                    Get<CBSCurrencyModule>().ChangeRequest(price.CurrencyID);
                    result?.Invoke(new CBSPurchaseAvatarResult
                    {
                        IsSuccess = true,
                        AvatarPrice = price,
                        PurchasedAvatarID = functionResult.PurchasedAvatarID,
                        UpdatedStates = functionResult.UpdatedStates
                    });
                }
            },
            onFailed =>
            {
                result?.Invoke(new CBSPurchaseAvatarResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get information abount linked accounts of profile
        /// </summary>
        /// <param name="result"></param>
        public void GetAccountLinkedInfo(Action<CBSGetAccountLinkedInfoResult> result)
        {
            FabProfile.GetCombinedAccountInfo(ProfileID, onGet =>
            {
                var fabInfo = onGet.AccountInfo;
                var accountsInfo = ParseLinkedInfo(fabInfo);
                CacheLinkedInfo(accountsInfo);
                result?.Invoke(new CBSGetAccountLinkedInfoResult
                {
                    IsSuccess = true,
                    Info = accountsInfo
                });
            }, onFailed =>
            {
                result?.Invoke(new CBSGetAccountLinkedInfoResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Call to update online status manually, you need to call it, then you have selected "OnlineBehavior" as "Custom" in your profile settings.
        /// </summary>
        /// <param name="result"></param>
        public void UpdateOnlineState(Action<CBSBaseResult> result = null)
        {
            var minisecondsThreshold = OnlineThreshold * 1000;
            FabProfile.UpdateProfileOnlineState(ProfileID, minisecondsThreshold, onUpdate =>
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
                        IsSuccess = true,
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
        /// Ban the user for a while. During the ban, the player will not be able to log in.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void BanProfile(CBSBanProfileRequest request, Action<CBSBanProfileResult> result)
        {
            var profile = request.ProfileIDToBan;
            var reason = request.Reason;
            var hours = request.BanHours;

            FabProfile.BanProfile(profile, reason, hours, onBan =>
            {
                var cbsError = onBan.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBanProfileResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var executeResult = onBan.GetResult<BanDetail>();
                    result?.Invoke(new CBSBanProfileResult
                    {
                        IsSuccess = true,
                        BanInfo = executeResult
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBanProfileResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Revoke all active bans from the profile.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        public void RevokeAllProfileBans(string profileID, Action<CBSBaseResult> result)
        {
            FabProfile.RevokeAllBans(profileID, onRemove =>
            {
                var cbsError = onRemove.GetCBSError();
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
        /// Revoke individual ban using BanID for profile.
        /// </summary>
        /// <param name="banID"></param>
        /// <param name="result"></param>
        public void RevokeProfileBan(string banID, Action<CBSBaseResult> result)
        {
            FabProfile.RevokeProfileBan(banID, onRemove =>
            {
                var cbsError = onRemove.GetCBSError();
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
        /// Get information about all ban for profile.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        public void GetProfileBanList(string profileID, Action<CBSBanListResult> result)
        {
            FabProfile.GetProfileBans(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBanListResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var resultData = onGet.GetResult<FunctionBanResult>();
                    result?.Invoke(new CBSBanListResult
                    {
                        IsSuccess = true,
                        BanList = resultData.BanList
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBanListResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get avatar id of current profile
        /// </summary>
        /// <param name="result"></param>
        public void GetProfileAvatarID(Action<CBSGetProfileAvatarIDResult> result)
        {
            GetProfileAvatarID(ProfileID, result);
        }

        /// <summary>
        /// Get avatar id by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        public void GetProfileAvatarID(string profileID, Action<CBSGetProfileAvatarIDResult> result)
        {
            FabProfile.GetProfileAvatarID(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetProfileAvatarIDResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var resultData = onGet.GetResult<ExecuteResult<string>>();
                    var avatarID = resultData.Result;
                    result?.Invoke(new CBSGetProfileAvatarIDResult
                    {
                        IsSuccess = true,
                        AvatarID = avatarID
                    });
                }
            },
            onFailed =>
            {
                result?.Invoke(new CBSGetProfileAvatarIDResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get all avatars available for profile
        /// </summary>
        /// <param name="result"></param>
        public void GetProfileAvatarTable(Action<CBSGetProfileAvatarTableResult> result)
        {
            FabProfile.GetProfileAvatarTable(onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetProfileAvatarTableResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var avatarTable = onGet.GetResult<AvatarsTable>();
                    result?.Invoke(new CBSGetProfileAvatarTableResult
                    {
                        IsSuccess = true,
                        Table = avatarTable
                    });
                }
            },
            onFailed =>
            {
                result?.Invoke(new CBSGetProfileAvatarTableResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get all avatars available for profile with states (available, purchased)
        /// </summary>
        /// <param name="result"></param>
        public void GetProfileAvatarTableWithStates(Action<CBSGetProfileAvatarTableWithStatesResult> result)
        {
            FabProfile.GetProfileAvatarTableWithStates(ProfileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetProfileAvatarTableWithStatesResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var avatarTable = onGet.GetResult<AvatarsTableWithStates>();
                    result?.Invoke(new CBSGetProfileAvatarTableWithStatesResult
                    {
                        IsSuccess = true,
                        TableWithStates = avatarTable
                    });
                }
            },
            onFailed =>
            {
                result?.Invoke(new CBSGetProfileAvatarTableWithStatesResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Delete master player account and all information associated with this account
        /// </summary>
        /// <param name="result"></param>
        public void DeleteMasterPlayerAccount(Action<CBSBaseResult> result)
        {
            FabProfile.DeleteMasterPlayerAccount( onDelete =>
                {
                    var cbsError = onDelete.GetCBSError();
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
                        Auth.Logout();
                    }
                },
                onFailed =>
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = CBSError.FromTemplate(onFailed)
                    });
                });
        }

        // internal

        private void InternalGetProfileData(string profileID, string[] keys, Action<CBSGetProfileDataResult> result)
        {
            FabProfile.GetProfileData(profileID, keys, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetProfileDataResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<GetUserDataResult>();
                    var data = functionResult.Data;
                    if (ProfileID == profileID)
                    {
                        ParseUserData(data);
                    }
                    result?.Invoke(new CBSGetProfileDataResult
                    {
                        IsSuccess = true,
                        Data = data
                    });
                }
            },
            onFailed =>
            {
                result?.Invoke(new CBSGetProfileDataResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        internal void ParseUserData(Dictionary<string, UserDataRecord> data)
        {
            if (data == null)
                return;
            CachedProfileData = CachedProfileData ?? new Dictionary<string, UserDataRecord>();
            foreach (var dataPair in data)
            {
                CachedProfileData[dataPair.Key] = dataPair.Value;
            }
        }

        internal AccountLinkedInfo ParseLinkedInfo(UserAccountInfo info)
        {
            return new AccountLinkedInfo
            {
                AppleLinked = info.AppleAccountInfo != null,
                FacebookLinked = info.FacebookInfo != null,
                GoogleLinked = info.GoogleInfo != null,
                SteamLinked = info.SteamInfo != null
            };
        }

        internal void CacheLinkedInfo(AccountLinkedInfo info)
        {
            LinkedAccounts = info;
        }

        internal LevelInfo ParseLevelData(string rawData, bool updateCache = true)
        {
            LevelInfo levelInfo = string.IsNullOrEmpty(rawData) ? new LevelInfo() : JsonPlugin.FromJsonDecompress<LevelInfo>(rawData);
            if (updateCache)
                CachedLevelInfo = levelInfo;
            return levelInfo;
        }

        internal LevelInfo ParseLevelData(LevelInfo info)
        {
            CachedLevelInfo = info;
            return info;
        }

        internal void ParseAvatarID(string avatarID)
        {
            Avatar = Avatar ?? new AvatarInfo();
            Avatar.AvatarID = avatarID;
        }

        internal void ParseClanInfo(string clanID, ClanEntity clanEntity)
        {
            ClanID = clanID;
            ClanEntity = clanEntity;
            ExistInClan = !string.IsNullOrEmpty(clanID);
        }

        internal void ParseAvatarUrl(string avatarURL)
        {
            Avatar = Avatar ?? new AvatarInfo();
            Avatar.AvatarURL = avatarURL;
        }

        internal void ParseLoginResult(CBSLoginResult resultData)
        {
            var loginResult = resultData.Result;
            var profile = loginResult.InfoResultPayload.PlayerProfile;
            var avatarUrl = profile == null ? string.Empty : profile.AvatarUrl;
            ParseAvatarUrl(avatarUrl);
            ProfileID = loginResult.PlayFabId;
            EntityID = loginResult.EntityToken.Entity.Id;
            EntityType = loginResult.EntityToken.Entity.Type;
            AuthenticationContex = loginResult.AuthenticationContext;
        }

        internal void ParseAccountInfo(UserAccountInfo resultData)
        {
            Avatar = Avatar ?? new AvatarInfo();
            var profileData = resultData.TitleInfo;
            Avatar.AvatarURL = profileData.AvatarUrl;
            DisplayName = profileData.DisplayName;
            RegistrationDate = profileData.Created;
        }

        internal void NotifyAboutChangingLinkedAccount(CredentialType type, bool isLinked)
        {
            var linkedInfo = LinkedAccounts;
            if (type == CredentialType.FACEBOOK)
                linkedInfo.FacebookLinked = isLinked;
            if (type == CredentialType.GOOGLE)
                linkedInfo.GoogleLinked = isLinked;
            if (type == CredentialType.APPLE)
                linkedInfo.AppleLinked = isLinked;
            if (type == CredentialType.STEAM)
                linkedInfo.SteamLinked = isLinked;
            CacheLinkedInfo(linkedInfo);
            OnLinkedAccountInfoChanged?.Invoke(linkedInfo);
        }

        protected override void OnLogout()
        {
            CachedLevelInfo = new LevelInfo();
            CachedProfileData = new Dictionary<string, UserDataRecord>();
            ProfileID = string.Empty;
            DisplayName = string.Empty;
            Avatar = null;
            EntityID = string.Empty;
            EntityType = string.Empty;
            AuthenticationContex = null;
            var enabledOnlineStatus = ProfileData.EnableOnlineStatus;
            if (enabledOnlineStatus)
            {
                OnlineProcessor?.Dispose();
                OnlineProcessor = null;
            }
        }

        // events
        private void OnProfileLoggedIn(CBSLoginResult result)
        {
            if (result.IsSuccess)
            {
                ParseLoginResult(result);
                var preloadAccountInfo = AuthData.PreloadAccountInfo;
                if (preloadAccountInfo)
                {
                    ParseAccountInfo(result.Result.InfoResultPayload.AccountInfo);
                    var linkedInfo = ParseLinkedInfo(result.Result.InfoResultPayload.AccountInfo);
                    CacheLinkedInfo(linkedInfo);
                }
                var preloadUserData = AuthData.PreloadUserData;
                if (preloadUserData)
                {
                    ParseUserData(result.Result.InfoResultPayload.UserData);
                }
                var enabledOnlineStatus = ProfileData.EnableOnlineStatus;
                if (enabledOnlineStatus)
                {
                    OnlineProcessor = new OnlineStatusProcessor(this, ProfileData, CoroutineRunner);
                    OnlineProcessor.StartUpdate();
                    UpdateOnlineState();
                }
            }
        }
    }
}
