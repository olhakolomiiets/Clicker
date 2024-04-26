using Azure.Data.Tables;
using CBS.Models;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CBS
{
    public class TableProfileAssistant
    {
        private static readonly string ProfileTableID = "CBSProfileTable";
        private static readonly string ProfilePartitionKey = "CBSProfile";

        private static readonly string DisplayNameKey = "DisplayName";
        private static readonly string LevelKey = "Level";
        private static readonly string ExpirienceKey = "Expirience";
        private static readonly string AvatarUrlKey = "AvatarUrl";
        private static readonly string AvatarIDKey = "AvatarID";
        private static readonly string ClanIDKey = "ClanID";
        private static readonly string StatisitcsKey = "Statistics";
        private static readonly string LastOnlineKey = "LastOnline";
        private static readonly string OnlineThresholdKey = "OnlineThreshold";
        private static readonly string ProfileDataKey = "ProfileData";
        private static readonly string RowKey = "RowKey";

        public static async Task<ExecuteResult<ProfileEntity>> GetProfileDetailAsync(string profileID, CBSProfileConstraints constraints)
        {
            var entityResult = await CosmosTable.GetEntityAsync(ProfileTableID, ProfilePartitionKey, profileID, GetKeysFromConstraints(constraints));
            if (entityResult.Error != null)
            {
                return ErrorHandler.ThrowError<ProfileEntity>(entityResult.Error);
            }
            var entity = entityResult.Result;
            var profileEntity = ParseFromEntity(entity);
            var loadClan = constraints.LoadClan;
            if (loadClan)
            {
                var clanID = profileEntity.ClanID;
                if (!string.IsNullOrEmpty(clanID))
                {
                    var clanEntityResult = await TableClanAssistant.GetClanDetailAsync(clanID, CBSClanConstraints.Full());
                    if (clanEntityResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<ProfileEntity>(clanEntityResult.Error);
                    }
                    var clanEntity = clanEntityResult.Result;
                    profileEntity.ClanEntity = clanEntity;
                }
            }
            return new ExecuteResult<ProfileEntity>
            {
                Result = profileEntity
            };
        }

        public static async Task<ExecuteResult<Dictionary<string, ProfileEntity>>> GetProfilesDetailsAsync(string [] profilesIDs, CBSProfileConstraints constraints)
        {
            var tasks = new List<Task<ExecuteResult<ProfileEntity>>>();
            foreach (var profileID in profilesIDs)
            {
                var task = GetProfileDetailAsync(profileID, constraints ?? new CBSProfileConstraints());
                tasks.Add(task);
            }
            try
            {
                var results = await Task.WhenAll(tasks);
                var profileList = results.Where(x=>x.Result != null).Select(x=>x.Result).ToDictionary(t => t.ProfileID, t=> t);
                return new ExecuteResult<Dictionary<string, ProfileEntity>>
                {
                    Result = profileList
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<Dictionary<string, ProfileEntity>>(err);
            }
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateProfileDisplayNameAsync(string profileID, string name)
        {
            var profileEntity = GetProfileEntity(profileID);
            profileEntity[DisplayNameKey] = name;
            var updateResult = await CosmosTable.UpsertEntityAsync(ProfileTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanIDAsync(string profileID, string clanID)
        {
            var profileEntity = GetProfileEntity(profileID);
            profileEntity[ClanIDKey] = clanID;
            var updateResult = await CosmosTable.UpsertEntityAsync(ProfileTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateProfileAvatarURLAsync(string profileID, string url)
        {
            var profileEntity = GetProfileEntity(profileID);
            profileEntity[AvatarUrlKey] = url;
            var updateResult = await CosmosTable.UpsertEntityAsync(ProfileTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateProfileExpirienceAsync(string profileID, int level, int expirience)
        {
            var profileEntity = GetProfileEntity(profileID);
            profileEntity[LevelKey] = level;
            profileEntity[ExpirienceKey] = expirience;
            var updateResult = await CosmosTable.UpsertEntityAsync(ProfileTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateProfileStatisticAsync(string profileID, string statisticName, int statValue, int position)
        {
            var profileResult = await GetProfileDetailAsync(profileID, new CBSProfileConstraints{LoadStatistics = true});
            if (profileResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(profileResult.Error);
            }
            var profile = profileResult.Result;
            var statistics = profile.Statistics.Statistics;
            statistics = statistics ?? new Dictionary<string, StatisticEntryInfo>();
            statistics[statisticName] = new StatisticEntryInfo
            {
                Position = position,
                Value = statValue
            };
            var statisticInfo = new StatisticsInfo
            {
                Statistics = statistics
            };
            var statisticRaw = JsonConvert.SerializeObject(statisticInfo);
            var profileEntity = GetProfileEntity(profileID);
            profileEntity[StatisitcsKey] = statisticRaw;
            var updateResult = await CosmosTable.UpsertEntityAsync(ProfileTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> ResetProfileStatisticAsync(string profileID)
        {
            var statisticRaw = JsonConvert.SerializeObject(new StatisticsInfo());
            var profileEntity = GetProfileEntity(profileID);
            profileEntity[StatisitcsKey] = statisticRaw;
            var updateResult = await CosmosTable.UpsertEntityAsync(ProfileTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateProfileDataAsync(string profileID, string key, string data)
        {
            var profileResult = await GetProfileDetailAsync(profileID, new CBSProfileConstraints{LoadProfileData = true});
            if (profileResult.Error != null)
            {
                profileResult.Result = new ProfileEntity();
                profileResult.Result.ProfileData = new DataInfo();
                //return ErrorHandler.ThrowError<Azure.Response>(profileResult.Error);
            }
            var profile = profileResult.Result;
            var profileData = profile.ProfileData.Data;
            profileData = profileData ?? new Dictionary<string, string>();
            profileData[key] = data;
            var dataInfo = new DataInfo
            {
                Data = profileData
            };
            var dataRaw = JsonConvert.SerializeObject(dataInfo);
            var profileEntity = GetProfileEntity(profileID);
            profileEntity[ProfileDataKey] = dataRaw;
            var updateResult = await CosmosTable.UpsertEntityAsync(ProfileTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateProfileOnlineStateAsync(string profileID, int threshold)
        {
            var profileEntity = GetProfileEntity(profileID);
            var timestamp = BaseAzureModule.ServerTimestamp;
            var onlineThreshold = timestamp + threshold;
            profileEntity[LastOnlineKey] = timestamp;
            profileEntity[OnlineThresholdKey] = onlineThreshold;
            var updateResult = await CosmosTable.UpsertEntityAsync(ProfileTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateProfileAvatarIDAsync(string profileID, string avatarID)
        {
            var profileEntity = GetProfileEntity(profileID);
            profileEntity[AvatarIDKey] = avatarID;
            var updateResult = await CosmosTable.UpsertEntityAsync(ProfileTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> RemoveProfileEntryAsync(string profileID)
        {
            var deleteResult = await CosmosTable.DeleteEntityAsync(ProfileTableID, profileID, ProfilePartitionKey);
            if (deleteResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(deleteResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = deleteResult.Result
            };
        }

        private static TableEntity GetProfileEntity(string profileID)
        {
            return new TableEntity{
                RowKey = profileID,
                PartitionKey = ProfilePartitionKey
            };
        }

        private static string[] GetKeysFromConstraints(CBSProfileConstraints constraints)
        {
            var keys = new List<string>();
            keys.Add(DisplayNameKey);
            keys.Add(RowKey);
            if (constraints.LoadAvatar) 
            {
                keys.Add(AvatarIDKey);
                keys.Add(AvatarUrlKey);
            }
            if (constraints.LoadClan)
            {
                keys.Add(ClanIDKey);
            }
            if (constraints.LoadLevel)
            {
                keys.Add(LevelKey);
                keys.Add(ExpirienceKey);
            }
            if (constraints.LoadStatistics)
            {
                keys.Add(StatisitcsKey);
            }
            if (constraints.LoadProfileData)
            {
                keys.Add(ProfileDataKey);
            }
            if (constraints.LoadOnlineStatus)
            {
                keys.Add(OnlineThresholdKey);
                keys.Add(LastOnlineKey);
            }
            return keys.ToArray();
        } 

        private static ProfileEntity ParseFromEntity(TableEntity entity)
        {
            if (entity == null)
                return new ProfileEntity();
            var profileID = entity.RowKey;
            var displayName = entity.GetString(DisplayNameKey);
            
            var avatarInfo = new AvatarInfo
            {
                AvatarID = entity.GetString(AvatarIDKey),
                AvatarURL = entity.GetString(AvatarUrlKey)
            };

            var clanID = entity.GetString(ClanIDKey);

            var levelInfo = new EntityLevelInfo
            {
                Expirience = entity.GetInt32(ExpirienceKey),
                Level = entity.GetInt32(LevelKey)
            };

            var onlineStatus = new OnlineStatusData();
            var lastOnline = entity.GetInt64(LastOnlineKey);
            var onlineThreshold = entity.GetInt64(OnlineThresholdKey);
            if (lastOnline != null && onlineThreshold != null)
            {
                var serverTimestamp = BaseAzureModule.ServerTimestamp;
                onlineStatus.IsOnline = serverTimestamp < onlineThreshold;
                onlineStatus.LastUpdate = BaseAzureModule.TimeStampToDate((long)lastOnline);
                onlineStatus.ServerTime = BaseAzureModule.TimeStampToDate(serverTimestamp);
                if (!onlineStatus.IsOnline)
                {
                    var milisecondsSpan = serverTimestamp - onlineThreshold;
                    onlineStatus.LastSeenOnlineTimeStamp = (long)milisecondsSpan;
                }
            }

            var ststisitcsRawData = entity.GetString(StatisitcsKey);
            ststisitcsRawData = string.IsNullOrEmpty(ststisitcsRawData) ? "{}" : ststisitcsRawData;
            var statistics = JsonConvert.DeserializeObject<StatisticsInfo>(ststisitcsRawData);

            var profileRawData = entity.GetString(ProfileDataKey);
            profileRawData = string.IsNullOrEmpty(profileRawData) ? "{}" : profileRawData;
            var profileData = JsonConvert.DeserializeObject<DataInfo>(profileRawData);

            return new ProfileEntity
            {
                ProfileID = profileID,
                DisplayName = displayName,
                Avatar = avatarInfo,
                ClanID = clanID,
                Level = levelInfo,
                Statistics = statistics,
                ProfileData = profileData,
                OnlineStatus = onlineStatus
            };
        }
    }
}