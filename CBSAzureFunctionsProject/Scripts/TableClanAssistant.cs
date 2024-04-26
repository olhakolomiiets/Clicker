using Azure.Data.Tables;
using CBS.Models;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CBS
{
    public class TableClanAssistant
    {
        private static readonly string ClanTableID = "CBSClanTable";
        private static readonly string ClanTaskTableID = "CBSClanTaskTable";
        private static readonly string ClanPartitionKey = "CBSClan";
        private static readonly string ClanTaskPartitionKey = "CBSClanTask";

        private static readonly string DisplayNameKey = "DisplayName";
        private static readonly string LevelKey = "Level";
        private static readonly string ExpirienceKey = "Expirience";
        private static readonly string AvatarKey = "Avatar";
        private static readonly string StatisitcsKey = "Statistics";
        private static readonly string ClanDataKey = "ClanData";
        private static readonly string ClanTaskStateKey = "TaskState";
        private static readonly string MembersCountKey = "MembersCount";
        private static readonly string VisibilityKey = "Visibility";
        private static readonly string DescriptionKey = "Description";
        private static readonly string RowKey = "RowKey";

        public static async Task<ExecuteResult<ClanEntity>> GetClanDetailAsync(string clanID, CBSClanConstraints constraints)
        {
            var entityResult = await CosmosTable.GetEntityAsync(ClanTableID, ClanPartitionKey, clanID, GetKeysFromConstraints(constraints));
            if (entityResult.Error != null)
            {
                return ErrorHandler.ThrowError<ClanEntity>(entityResult.Error);
            }
            var entity = entityResult.Result;
            return new ExecuteResult<ClanEntity>
            {
                Result = ParseFromEntity(entity)
            };
        }

        public static async Task<ExecuteResult<Dictionary<string, ClanEntity>>> GetClansDetailsAsync(string [] clanIDs, CBSClanConstraints constraints)
        {
            var tasks = new List<Task<ExecuteResult<ClanEntity>>>();
            foreach (var profileID in clanIDs)
            {
                var task = GetClanDetailAsync(profileID, constraints ?? new CBSClanConstraints());
                tasks.Add(task);
            }
            try
            {
                var results = await Task.WhenAll(tasks);
                var clanList = results.Where(x=>x.Result != null).Select(x=>x.Result).ToDictionary(t => t.ClanID, t=> t);
                return new ExecuteResult<Dictionary<string, ClanEntity>>
                {
                    Result = clanList
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<Dictionary<string, ClanEntity>>(err);
            }
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanVisibilityAsync(string clanID, ClanVisibility visibility)
        {
            var clanEntity = GetClanEntity(clanID);
            clanEntity[VisibilityKey] = visibility.ToString();
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTableID, clanEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanMembersCountAsync(string clanID, int membersCount)
        {
            var clanEntity = GetClanEntity(clanID);
            clanEntity[MembersCountKey] = membersCount;
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTableID, clanEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanDescriptionAsync(string clanID, string description)
        {
            var clanEntity = GetClanEntity(clanID);
            clanEntity[DescriptionKey] = description;
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTableID, clanEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanDisplayNameAsync(string clanID, string name)
        {
            var clanEntity = GetClanEntity(clanID);
            clanEntity[DisplayNameKey] = name;
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTableID, clanEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanExpirienceAsync(string clanID, int level, int expirience)
        {
            var clanEntity = GetClanEntity(clanID);
            clanEntity[LevelKey] = level;
            clanEntity[ExpirienceKey] = expirience;
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTableID, clanEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanStatisticAsync(string clanID, string statisticName, int statValue, int position)
        {
            var profileResult = await GetClanDetailAsync(clanID, new CBSClanConstraints{LoadStatistics = true});
            if (profileResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(profileResult.Error);
            }
            var clan = profileResult.Result;
            var statistics = clan.Statistics.Statistics;
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
            var clanEntity = GetClanEntity(clanID);
            clanEntity[StatisitcsKey] = statisticRaw;
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTableID, clanEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> ResetClanStatisticAsync(string clanID)
        {
            var statisticRaw = JsonConvert.SerializeObject(new StatisticsInfo());
            var clanEntity = GetClanEntity(clanID);
            clanEntity[StatisitcsKey] = statisticRaw;
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTableID, clanEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanDataAsync(string clanID, string key, string data)
        {
            var clanResult = await GetClanDetailAsync(clanID, new CBSClanConstraints{LoadClanData = true});
            if (clanResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(clanResult.Error);
            }
            var clan = clanResult.Result;
            var clanData = clan.ClanData.Data;
            clanData = clanData ?? new Dictionary<string, string>();
            clanData[key] = data;
            var dataInfo = new DataInfo
            {
                Data = clanData
            };
            var dataRaw = JsonConvert.SerializeObject(dataInfo);
            var clanEntity = GetClanEntity(clanID);
            clanEntity[ClanDataKey] = dataRaw;
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTableID, clanEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanDataAsync(string clanID, Dictionary<string, string> customData)
        {
            var clanResult = await GetClanDetailAsync(clanID, new CBSClanConstraints{LoadClanData = true});
            if (clanResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(clanResult.Error);
            }
            var clan = clanResult.Result;
            var clanData = clan.ClanData.Data;
            clanData = clanData ?? new Dictionary<string, string>();
            foreach (var dataPair in customData)
            {
                clanData[dataPair.Key] = dataPair.Value;
            }
            var dataInfo = new DataInfo
            {
                Data = clanData
            };
            var dataRaw = JsonConvert.SerializeObject(dataInfo);
            var clanEntity = GetClanEntity(clanID);
            clanEntity[ClanDataKey] = dataRaw;
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTableID, clanEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanAvatarAsync(string clanID, ClanAvatarInfo avatarInfo)
        {
            var profileEntity = GetClanEntity(clanID);
            var avatarRawData = JsonConvert.SerializeObject(avatarInfo);
            profileEntity[AvatarKey] = avatarRawData;
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateClanTasksStateAsync(string clanID, Dictionary<string, BaseTaskState> states)
        {
            var profileEntity = GetClanTaskEntity(clanID);
            var stateRawData = JsonPlugin.ToJsonCompress(states);
            profileEntity[ClanTaskStateKey] = stateRawData;
            var updateResult = await CosmosTable.UpsertEntityAsync(ClanTaskTableID, profileEntity);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(updateResult.Error);
            }
            return new ExecuteResult<Azure.Response>
            {
                Result = updateResult.Result
            };
        }

        public static async Task<ExecuteResult<Dictionary<string, BaseTaskState>>> GetClanTaskState(string clanID)
        {
            var entityResult = await CosmosTable.GetEntityAsync(ClanTaskTableID, ClanTaskPartitionKey, clanID, new string [] { ClanTaskStateKey });
            if (entityResult.Error != null)
            {
                return ErrorHandler.ThrowError<ClanEntity>(entityResult.Error);
            }
            var entity = entityResult.Result;
            var stateRawData = entity.GetString(ClanTaskStateKey) ?? JsonPlugin.EMPTY_JSON;
            var states = JsonPlugin.FromJsonDecompress<Dictionary<string, BaseTaskState>>(stateRawData);

            return new ExecuteResult<Dictionary<string, BaseTaskState>>
            {
                Result = states
            };
        }

        public static async Task<ExecuteResult<Azure.Response>> DeleteClanAsync(string clanID)
        {
            var deleteResult = await CosmosTable.DeleteEntityAsync(ClanTableID, clanID, ClanPartitionKey);
            if (deleteResult.Error != null)
            {
                return ErrorHandler.ThrowError<Azure.Response>(deleteResult.Error);
            }

            return new ExecuteResult<Azure.Response>
            {
                Result = deleteResult.Result
            };
        }

        private static TableEntity GetClanEntity(string clanID)
        {
            return new TableEntity{
                RowKey = clanID,
                PartitionKey = ClanPartitionKey
            };
        }

        private static TableEntity GetClanTaskEntity(string clanID)
        {
            return new TableEntity{
                RowKey = clanID,
                PartitionKey = ClanTaskPartitionKey
            };
        }

        private static string[] GetKeysFromConstraints(CBSClanConstraints constraints)
        {
            var keys = new List<string>();
            keys.Add(DisplayNameKey);
            keys.Add(RowKey);
            if (constraints.LoadAvatar) 
            {
                keys.Add(AvatarKey);
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
            if (constraints.LoadClanData)
            {
                keys.Add(ClanDataKey);
            }
            if (constraints.LoadMembersCount)
            {
                keys.Add(MembersCountKey);
            }
            if (constraints.LoadVisibility)
            {
                keys.Add(VisibilityKey);
            }
            if (constraints.LoadDescription)
            {
                keys.Add(DescriptionKey);
            }

            return keys.ToArray();
        } 

        private static ClanEntity ParseFromEntity(TableEntity entity)
        {
            if (entity == null)
                return new ClanEntity();
            var clanID = entity.RowKey;
            var displayName = entity.GetString(DisplayNameKey);
            var description = entity.GetString(DescriptionKey);

            var visibilityString = entity.GetString(VisibilityKey);
            var visibility = ClanVisibility.OPEN;
            Enum.TryParse<ClanVisibility>(visibilityString, out visibility);

            var membersCount = entity.GetInt32(MembersCountKey).GetValueOrDefault();

            var levelInfo = new EntityLevelInfo
            {
                Expirience = entity.GetInt32(ExpirienceKey),
                Level = entity.GetInt32(LevelKey)
            };
            
            var clanAvatarRawData = entity.GetString(AvatarKey);
            clanAvatarRawData = string.IsNullOrEmpty(clanAvatarRawData) ? JsonPlugin.EMPTY_JSON : clanAvatarRawData;
            var avatarInfo = JsonConvert.DeserializeObject<ClanAvatarInfo>(clanAvatarRawData);

            var ststisitcsRawData = entity.GetString(StatisitcsKey);
            ststisitcsRawData = string.IsNullOrEmpty(ststisitcsRawData) ? JsonPlugin.EMPTY_JSON : ststisitcsRawData;
            var statistics = JsonConvert.DeserializeObject<StatisticsInfo>(ststisitcsRawData);

            var profileRawData = entity.GetString(ClanDataKey);
            profileRawData = string.IsNullOrEmpty(profileRawData) ? JsonPlugin.EMPTY_JSON : profileRawData;
            var profileData = JsonConvert.DeserializeObject<DataInfo>(profileRawData);

            return new ClanEntity
            {
                ClanID = clanID,
                DisplayName = displayName,
                Description = description,
                Avatar = avatarInfo,
                LevelInfo = levelInfo,
                Statistics = statistics,
                ClanData = profileData,
                Visibility = visibility,
                MembersCount = membersCount
            };
        }
    }
}