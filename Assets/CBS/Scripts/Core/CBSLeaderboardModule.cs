using CBS.Models;
using CBS.Playfab;
using CBS.Scriptable;
using CBS.Utils;
using System;

namespace CBS
{
    public class CBSLeaderboardModule : CBSModule, ILeaderboard
    {
        private IFabLeaderboard FabLeaderboard { get; set; }
        private IProfile Profile { get; set; }
        private ProfileConfigData ProfileConfig { get; set; }

        protected override void Init()
        {
            FabLeaderboard = FabExecuter.Get<FabLeaderboard>();
            Profile = Get<CBSProfileModule>();
            ProfileConfig = CBSScriptable.Get<ProfileConfigData>();
        }

        /// <summary>
        /// Get users leaderboard based by player level/xp
        /// </summary>
        /// <param name="result"></param>
        public void GetLevelLeadearboard(CBSGetLevelLeaderboardRequest request, Action<CBSGetLeaderboardResult> result)
        {
            string profileID = Profile.ProfileID;
            var leaderboardRequest = request.ToLeaderboardRequest();
            InternalGetProfileLeaderboard(profileID, leaderboardRequest, result);
        }

        /// <summary>
        /// Get leaderboard around user based by player level/xp
        /// </summary>
        /// <param name="result"></param>
        public void GetLevelLeadearboardAroundProfile(CBSGetLevelLeaderboardRequest request, Action<CBSGetLeaderboardResult> result)
        {
            string profileID = Profile.ProfileID;
            var leaderboardRequest = request.ToLeaderboardRequest();
            InternalGetLeaderboardAroundProfile(profileID, leaderboardRequest, result);
        }

        /// <summary>
        /// Get users leaderboard based on custom statistics.
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="result"></param>
        public void GetLeadearboard(CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result)
        {
            string profileID = Profile.ProfileID;
            InternalGetProfileLeaderboard(profileID, request, result);
        }

        /// <summary>
        /// Get leaderboard by profile id
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void GetLeadearboardByProfileID(string profileID, CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result)
        {
            InternalGetProfileLeaderboard(profileID, request, result);
        }

        /// <summary>
        /// Get leaderboard around user based on custom statistics.
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="result"></param>
        public void GetLeadearboardAround(CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result)
        {
            string profileID = Profile.ProfileID;
            InternalGetLeaderboardAroundProfile(profileID, request, result);
        }

        /// <summary>
        /// Get leaderboard around players by profile id.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void GetLeadearboardAroundByProfileID(string profileID, CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result)
        {
            InternalGetLeaderboardAroundProfile(profileID, request, result);
        }

        /// <summary>
        /// Get friends leaderboard based by player level/xp
        /// </summary>
        /// <param name="result"></param>
        public void GetFriendsLeadearboard(CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result)
        {
            string profileID = Profile.ProfileID;
            var constraints = request.Constraints ?? GetDefaultProfileConstraints();
            request.Constraints = constraints;
            FabLeaderboard.GetFriendsLeaderboard(profileID, request, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetLeaderboardResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetLeaderboardResult>();
                    var leaderboard = functionResult.Leaderboard;
                    var nextResetDate = functionResult.NextReset;
                    var version = functionResult.Version;
                    var profileEntry = functionResult.ProfileEntry;

                    result?.Invoke(new CBSGetLeaderboardResult
                    {
                        IsSuccess = true,
                        Leaderboard = leaderboard,
                        NextReset = nextResetDate,
                        ProfileEntry = profileEntry,
                        Version = version
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetLeaderboardResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Update statisitc points by statistic name
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        public void UpdateStatisticPoint(string statisticName, int value, Action<CBSUpdateStatisticResult> result)
        {
            string profileID = Profile.ProfileID;

            FabLeaderboard.UpdateStatisticPoint(profileID, statisticName, value, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSUpdateStatisticResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onUpdate.GetResult<FunctionUpdateStatisticResult>();
                    var targetID = functionResult.ProfileID;
                    var leaderboardName = functionResult.StatisticName;
                    var newPosition = functionResult.StatisticPosition;
                    var newValue = functionResult.StatisticValue;

                    result?.Invoke(new CBSUpdateStatisticResult
                    {
                        IsSuccess = true,
                        ProfileID = targetID,
                        StatisticName = leaderboardName,
                        StatisticPosition = newPosition,
                        StatisticValue = newValue
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSUpdateStatisticResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Add statisitc points by statistic name
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        public void AddStatisticPoint(string statisticName, int value, Action<CBSUpdateStatisticResult> result)
        {
            string profileID = Profile.ProfileID;

            FabLeaderboard.AddStatisticPoint(profileID, statisticName, value, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSUpdateStatisticResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onUpdate.GetResult<FunctionUpdateStatisticResult>();
                    var targetID = functionResult.ProfileID;
                    var leaderboardName = functionResult.StatisticName;
                    var newPosition = functionResult.StatisticPosition;
                    var newValue = functionResult.StatisticValue;

                    result?.Invoke(new CBSUpdateStatisticResult
                    {
                        IsSuccess = true,
                        ProfileID = targetID,
                        StatisticName = leaderboardName,
                        StatisticPosition = newPosition,
                        StatisticValue = newValue
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSUpdateStatisticResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Reset all players statistics, include all leaderboards and player exp/level.
        /// </summary>
        /// <param name="result"></param>
        public void ResetAllProfileStatistics(Action<CBSBaseResult> result)
        {
            string profileID = Profile.ProfileID;

            FabLeaderboard.ResetProfileLeaderboards(profileID, onReset =>
            {
                var cbsError = onReset.GetCBSError();
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
        /// Get leaderboard around clan
        /// </summary>
        /// <param name="result"></param>
        public void GetLeaderboardAroundClan(string clanID, CBSGetClanLeaderboardRequest request, Action<CBSGetClanLeaderboardResult> result)
        {
            var constraints = request.Constraints ?? GetDefaultClanConstraints();
            request.Constraints = constraints;
            FabLeaderboard.GetLeaderboardAroundClan(clanID, request, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetClanLeaderboardResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetClanLeaderboardResult>();
                    var leaderboard = functionResult.Leaderboard;
                    var nextResetDate = functionResult.NextReset;
                    var version = functionResult.Version;
                    var clanEntry = functionResult.ClanEntry;

                    result?.Invoke(new CBSGetClanLeaderboardResult
                    {
                        IsSuccess = true,
                        Leaderboard = leaderboard,
                        NextReset = nextResetDate,
                        ClanEntry = clanEntry,
                        Version = version
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetClanLeaderboardResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get clans leaderboard
        /// </summary>
        /// <param name="result"></param>
        public void GetClanLeaderboard(CBSGetClanLeaderboardRequest request, Action<CBSGetClanLeaderboardResult> result)
        {
            var clanID = Profile.ClanID;
            var constraints = request.Constraints ?? GetDefaultClanConstraints();
            request.Constraints = constraints;
            FabLeaderboard.GetClanLeaderboard(clanID, request, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetClanLeaderboardResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetClanLeaderboardResult>();
                    var leaderboard = functionResult.Leaderboard;
                    var nextResetDate = functionResult.NextReset;
                    var version = functionResult.Version;
                    var clanEntry = functionResult.ClanEntry;

                    result?.Invoke(new CBSGetClanLeaderboardResult
                    {
                        IsSuccess = true,
                        Leaderboard = leaderboard,
                        NextReset = nextResetDate,
                        ClanEntry = clanEntry,
                        Version = version
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetClanLeaderboardResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Add clan statistic point of current profile clan
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        public void AddClanStatisticPoint(string statisticName, int value, Action<CBSUpdateClanStatisticResult> result)
        {
            string clanID = Profile.ClanID;
            string profileID = Profile.ProfileID;

            FabLeaderboard.AddClanStatisticPoint(profileID, clanID, statisticName, value, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSUpdateClanStatisticResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onUpdate.GetResult<FunctionUpdateClanStatisticResult>();
                    var targetID = functionResult.ClanID;
                    var leaderboardName = functionResult.StatisticName;
                    var newPosition = functionResult.StatisticPosition;
                    var newValue = functionResult.StatisticValue;

                    result?.Invoke(new CBSUpdateClanStatisticResult
                    {
                        IsSuccess = true,
                        ClanID = targetID,
                        StatisticName = leaderboardName,
                        StatisticPosition = newPosition,
                        StatisticValue = newValue
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSUpdateClanStatisticResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Update clan statistic point of current profile clan
        /// </summary>
        /// <param name="statisticName"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        public void UpdateClanStatisticPoint(string statisticName, int value, Action<CBSUpdateClanStatisticResult> result)
        {
            string clanID = Profile.ClanID;
            string profileID = Profile.ProfileID;

            FabLeaderboard.UpdateClanStatisticPoint(profileID, clanID, statisticName, value, onUpdate =>
            {
                var cbsError = onUpdate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSUpdateClanStatisticResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onUpdate.GetResult<FunctionUpdateClanStatisticResult>();
                    var targetID = functionResult.ClanID;
                    var leaderboardName = functionResult.StatisticName;
                    var newPosition = functionResult.StatisticPosition;
                    var newValue = functionResult.StatisticValue;

                    result?.Invoke(new CBSUpdateClanStatisticResult
                    {
                        IsSuccess = true,
                        ClanID = targetID,
                        StatisticName = leaderboardName,
                        StatisticPosition = newPosition,
                        StatisticValue = newValue
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSUpdateClanStatisticResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        // internal
        private void InternalGetProfileLeaderboard(string profileID, CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result)
        {
            var constraints = request.Constraints ?? GetDefaultProfileConstraints();
            request.Constraints = constraints;
            FabLeaderboard.GetLeaderboard(profileID, request, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetLeaderboardResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetLeaderboardResult>();
                    var leaderboard = functionResult.Leaderboard;
                    var nextResetDate = functionResult.NextReset;
                    var version = functionResult.Version;
                    var profileEntry = functionResult.ProfileEntry;

                    result?.Invoke(new CBSGetLeaderboardResult
                    {
                        IsSuccess = true,
                        Leaderboard = leaderboard,
                        NextReset = nextResetDate,
                        ProfileEntry = profileEntry,
                        Version = version
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetLeaderboardResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        private void InternalGetLeaderboardAroundProfile(string profileID, CBSGetLeaderboardRequest request, Action<CBSGetLeaderboardResult> result)
        {
            var constraints = request.Constraints ?? GetDefaultProfileConstraints();
            request.Constraints = constraints;
            FabLeaderboard.GetLeaderboardAroundProfile(profileID, request, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetLeaderboardResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetLeaderboardResult>();
                    var leaderboard = functionResult.Leaderboard;
                    var nextResetDate = functionResult.NextReset;
                    var version = functionResult.Version;
                    var profileEntry = functionResult.ProfileEntry;

                    result?.Invoke(new CBSGetLeaderboardResult
                    {
                        IsSuccess = true,
                        Leaderboard = leaderboard,
                        NextReset = nextResetDate,
                        ProfileEntry = profileEntry,
                        Version = version
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetLeaderboardResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
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

        private CBSClanConstraints GetDefaultClanConstraints()
        {
            var profileConfig = ProfileConfig.EnableOnlineStatus;
            return new CBSClanConstraints
            {
                LoadAvatar = true
            };
        }
    }
}