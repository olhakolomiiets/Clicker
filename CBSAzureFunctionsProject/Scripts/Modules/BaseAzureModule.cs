using PlayFab.ServerModels;
using PlayFab.GroupsModels;
using PlayFab.DataModels;
using PlayFab.Samples;
using PlayFab.AuthenticationModels;
using PlayFab.MultiplayerModels;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using PlayFab;
using System;
using CBS.Models;
using System.Linq;
using Azure.Storage.Blobs;
using Medallion.Threading.Azure;
using Medallion.Threading;
using Azure.Storage.Queues;
using Azure.Storage.Blobs.Models;
using Censored;

namespace CBS
{
    public class BaseAzureModule
    {
        private static readonly string PlayFabTitleKey = "PLAYFAB_TITLE_ID";
        private static readonly string PlayFabSecretKey = "PLAYFAB_DEV_SECRET_KEY";
        private static readonly string LockContainer = "cbslockcontainer";
        public const string StorageConnectionKey = "AzureWebJobsStorage";
        public const string CosmosTableConnectionKey = "TABLE_CONNECTION_STRING";

        public const string ProfileEntityKey = "title_player_account";
        public const string GroupEntityKey = "group";
        public const string LineageProfileAccountKey = "master_player_account";

        public static long ServerTimestamp
        {
            get => DateToTimestamp(ServerTimeUTC);
        }

        public static DateTime ServerTimeUTC
        {
            get => DateTime.UtcNow;
        }

        public static DateTime ServerTimeLocal
        {
            get => DateTime.Now;
        }

        public static int ServerTimezoneOffset
        {
            get
            {
                var curTimeZone = TimeZoneInfo.Local;
                var offset = curTimeZone.GetUtcOffset(DateTime.Now);
                return (int)offset.TotalMilliseconds;
            }
        }

        public static string TitleId
        {
            get => Environment.GetEnvironmentVariable(PlayFabTitleKey, EnvironmentVariableTarget.Process);
        }

        public static string SercetKey
        {
            get => Environment.GetEnvironmentVariable(PlayFabSecretKey, EnvironmentVariableTarget.Process);
        }

        public static string StorageConnectionString
        {
            get => Environment.GetEnvironmentVariable(StorageConnectionKey, EnvironmentVariableTarget.Process);
        }

        public static string CosmosConnectionString
        {
            get => Environment.GetEnvironmentVariable(CosmosTableConnectionKey, EnvironmentVariableTarget.Process);
        }
        
        // Playfab settings based on Azure environment variables
        private static PlayFabApiSettings FabSettingAPI = new PlayFabApiSettings   
        {  
            TitleId = TitleId, 
            DeveloperSecretKey = SercetKey
        };

        private static async Task<PlayFabAuthenticationContext> GetServerAuthContextAsync()
        {
            var entityTokenRequest = new GetEntityTokenRequest();
            var authApi = new PlayFabAuthenticationInstanceAPI(FabSettingAPI);
            var entityTokenResult = await authApi.GetEntityTokenAsync(entityTokenRequest);
            var authContext = new PlayFabAuthenticationContext{
                EntityId = entityTokenResult.Result.Entity.Id,
                EntityType = entityTokenResult.Result.Entity.Type,
                EntityToken = entityTokenResult.Result.EntityToken
            };
            return authContext;
        }

        private static PlayFabServerInstanceAPI fabServerAPI;
        public static PlayFabServerInstanceAPI FabServerAPI
        {
            get
            {
                if (fabServerAPI == null)
                    fabServerAPI = new PlayFabServerInstanceAPI(FabSettingAPI);
                return fabServerAPI;
            }
        }

        private static PlayFabClientInstanceAPI fabClientAPI;
        public static PlayFabClientInstanceAPI FabClientAPI
        {
            get
            {
                if (fabClientAPI == null)
                    fabClientAPI = new PlayFabClientInstanceAPI(FabSettingAPI);
                return fabClientAPI;
            }
        }

        private static PlayFabGroupsInstanceAPI fabGroupAPI;
        public static async Task<PlayFabGroupsInstanceAPI> GetFabGroupAPIAsync()
        {
            if (fabGroupAPI == null)
            {
                var auth = await GetServerAuthContextAsync();
                fabGroupAPI = new PlayFabGroupsInstanceAPI(FabSettingAPI, auth);
            }
            return fabGroupAPI;
        }

        private static PlayFabDataInstanceAPI fabDataAPI;
        public static async Task<PlayFabDataInstanceAPI> GetFabDataAPIAsync()
        {
            if (fabDataAPI == null)
            {
                var auth = await GetServerAuthContextAsync();
                fabDataAPI = new PlayFabDataInstanceAPI(FabSettingAPI, auth);
            }
            return fabDataAPI;
        }

        private static PlayFabMultiplayerInstanceAPI fabMultiplayerAPI;
        public static async Task<PlayFabMultiplayerInstanceAPI> GetFabMultiplayerAPIAsync()
        {
            if (fabMultiplayerAPI == null)
            {
                var auth = await GetServerAuthContextAsync();
                fabMultiplayerAPI = new PlayFabMultiplayerInstanceAPI(FabSettingAPI, auth);
            }
            return fabMultiplayerAPI;
        }

        private static PlayFabAdminInstanceAPI fabAdminAPI;
        public static async Task<PlayFabAdminInstanceAPI> GetFabAdminAPIAsync()
        {
            if (fabAdminAPI == null)
            {
                var auth = await GetServerAuthContextAsync();
                fabAdminAPI = new PlayFabAdminInstanceAPI(FabSettingAPI, auth);
            }
            return fabAdminAPI;
        }

        private static BlobContainerClient blobContainer;
        public static BlobContainerClient BlobContainer
        {
            get
            {
                if (blobContainer == null)
                    blobContainer = new BlobContainerClient(StorageConnectionString, LockContainer);
                return blobContainer;
            }
        }

        private static AzureBlobLeaseDistributedSynchronizationProvider lockProvider;
        public static AzureBlobLeaseDistributedSynchronizationProvider LockProvider
        {
            get
            {
                if (lockProvider == null)
                    lockProvider = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
                return lockProvider;
            }
        }

        private static Censor censor;
        public static Censor Censor
        {
            get
            {
                if (censor == null)
                {
                    var profanityList = ProfanityList.Profanity;
                    censor = new Censor(profanityList);
                }
                return censor;
            }
        }

        // playfab functions
        public static async Task<ExecuteResult<T>> GetTitleDataAsObjectAsync<T>(string titleKey) where T : class
        {
            var result = await GetRawTitleDataAsync(titleKey);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<T>(result.Error);
            }
            var rawData = result.Result;
            var dataObject = (string.IsNullOrEmpty(rawData)) ? null : JsonConvert.DeserializeObject<T>(rawData);
            return new ExecuteResult<T>
            {
                Result = dataObject
            };
        }

        public static async Task<ExecuteResult<T>> GetInternalTitleDataAsObjectAsync<T>(string titleKey) where T : class
        {
            var result = await GetRawInternalTitleDataAsync(titleKey);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<T>(result.Error);
            }
            var rawData = result.Result;
            if (string.IsNullOrEmpty(rawData))
            {
                return new ExecuteResult<T>
                {
                    Result = null
                };
            }
            try 
            {
                return new ExecuteResult<T>
                {
                    Result = JsonPlugin.FromJsonDecompress<T>(rawData)
                };
                
            }
            catch
            {
                return new ExecuteResult<T>
                {
                    Result = JsonPlugin.FromJson<T>(rawData)
                };
            }           
        }

        public static async Task<ExecuteResult<string>> GetRawTitleDataAsync(string titleKey)
        {
            var request = new GetTitleDataRequest{
                Keys = new List<string>() {titleKey}
            };
            var result = await FabServerAPI.GetTitleDataAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<string>(result.Error);
            }
            var data = result.Result.Data;
            var rawData = data.ContainsKey(titleKey) ? data[titleKey] : string.Empty;
            return new ExecuteResult<string>
            {
                Result = rawData
            };
        }

        public static async Task<ExecuteResult<string>> GetRawInternalTitleDataAsync(string titleKey)
        {
            var request = new GetTitleDataRequest{
                Keys = new List<string>() {titleKey}
            };
            var result = await FabServerAPI.GetTitleInternalDataAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<string>(result.Error);
            }
            var data = result.Result.Data;
            var rawData = data.ContainsKey(titleKey) ? data[titleKey] : string.Empty;
            return new ExecuteResult<string>
            {
                Result = rawData
            };
        }

        public static async Task<ExecuteResult<Dictionary<string, string>>> GetRawInternalTitleDataAsync(string [] titleKey)
        {
            var request = new GetTitleDataRequest{
                Keys = titleKey.ToList()
            };
            var result = await FabServerAPI.GetTitleInternalDataAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<string>(result.Error);
            }
            var data = result.Result.Data;
            return new ExecuteResult<Dictionary<string, string>>
            {
                Result = data
            };
        }

        public static async Task<ExecuteResult<Dictionary<string, string>>> GetRawTitlesDataAsync(List<string> titleKeys)
        {
            var request = new GetTitleDataRequest{
                Keys = titleKeys
            };
            var result = await FabServerAPI.GetTitleDataAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<string>(result.Error);
            }
            var data = result.Result.Data;
            return new ExecuteResult<Dictionary<string, string>>
            {
                Result = data
            };
        }

        public static async Task<ExecuteResult<Dictionary<string, string>>> GetRawInternalTitlesDataAsync(List<string> titleKeys)
        {
            var request = new GetTitleDataRequest{
                Keys = titleKeys
            };
            var result = await FabServerAPI.GetTitleInternalDataAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<string>(result.Error);
            }
            var data = result.Result.Data;
            return new ExecuteResult<Dictionary<string, string>>
            {
                Result = data
            };
        }

        public static async Task<ExecuteResult<string>> GetProfileInternalRawData(string profileID, string dataKey)
        {
            var resultData = await FabServerAPI.GetUserInternalDataAsync(new GetUserDataRequest { 
                PlayFabId = profileID,
                Keys = new List<string>() {dataKey} 
            });
            if (resultData.Error != null)
            {
                return ErrorHandler.ThrowError<string>(resultData.Error);
            }
            var data = resultData.Result.Data;
            var rawData = data.ContainsKey(dataKey) ? data[dataKey].Value : string.Empty;
            return new ExecuteResult<string>
            {
                Result = rawData
            };
        }

        public static async Task<ExecuteResult<T>> GetProfileInternalDataAsObject<T>(string profileID, string dataKey) where T : class
        {
            var result = await GetProfileInternalRawData(profileID, dataKey);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<T>(result.Error);
            }
            var rawData = result.Result ?? JsonPlugin.EMPTY_JSON;
            try
            {
                var dataObject = (string.IsNullOrEmpty(rawData)) ? null : JsonPlugin.FromJsonDecompress<T>(rawData);
                return new ExecuteResult<T>
                {
                    Result = dataObject
                };
            }
            catch
            {
                var dataObject = (string.IsNullOrEmpty(rawData)) ? null : JsonConvert.DeserializeObject<T>(rawData);
                return new ExecuteResult<T>
                {
                    Result = dataObject
                };
            }          
        }

        public static async Task<ExecuteResult<UpdateUserDataResult>> SaveProfileInternalDataAsync(string profileID, string dataKey, string dataValue)
        {
            var playerData = new Dictionary<string, string>();
            playerData[dataKey] = dataValue;
            
            var updateDataRequest = new UpdateUserInternalDataRequest {
                PlayFabId = profileID,
                Data = playerData
            };
            
            var updateResult = await FabServerAPI.UpdateUserInternalDataAsync(updateDataRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<UpdateUserDataResult>(updateResult.Error);
            }
            return new ExecuteResult<UpdateUserDataResult>
            {
                Result =  updateResult.Result 
            };
        }

        public static async Task<ExecuteResult<SetTitleDataResult>> SaveInternalTitleDataAsync(string dataKey, string dataValue)
        {
            var playerData = new Dictionary<string, string>();
            playerData[dataKey] = dataValue;
            
            var updateDataRequest = new SetTitleDataRequest {
                Key = dataKey,
                Value = dataValue
            };
            
            var updateResult = await FabServerAPI.SetTitleInternalDataAsync(updateDataRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<SetTitleDataResult>(updateResult.Error);
            }
            return new ExecuteResult<SetTitleDataResult>
            {
                Result =  updateResult.Result 
            };
        }

        public static async Task<ExecuteResult<GrantItemsToUserResult>> InternalGrantItemsToPlayerAsync(string catalogID, List<string> ItemIds, string profileID)
        {
            var request = new GrantItemsToUserRequest {
                PlayFabId = profileID,
                ItemIds = ItemIds,
                CatalogVersion = catalogID
            };
            
            var result = await FabServerAPI.GrantItemsToUserAsync(request);
            var hasError = result.Result == null || result.Result.ItemGrantResults == null ||  result.Result.ItemGrantResults.Any(x=>x == null || x.Result == false);
            if (hasError)
            {
                return ErrorHandler.ThrowGrantItemsToPlayerError<GrantItemsToUserResult>();
            }
            return new ExecuteResult<GrantItemsToUserResult>{
                Result = result.Result
            };
        }

        public static async Task<ExecuteResult<SetTitleDataResult>> SavePublicTitleDataAsync(string dataKey, string dataValue)
        {
            var playerData = new Dictionary<string, string>();
            playerData[dataKey] = dataValue;
            
            var updateDataRequest = new SetTitleDataRequest {
                Key = dataKey,
                Value = dataValue
            };
            
            var updateResult = await FabServerAPI.SetTitleDataAsync(updateDataRequest);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<SetTitleDataResult>(updateResult.Error);
            }
            return new ExecuteResult<SetTitleDataResult>
            {
                Result =  updateResult.Result 
            };
        }

        public static async Task<ExecuteResult<RevokeInventoryResult>> RemoveProfileInventoryItem(string profileID, string itemInstanceID)
        {
            var request = new RevokeInventoryItemRequest
            {
                PlayFabId = profileID,
                ItemInstanceId = itemInstanceID
            };
            var removeResult = await fabServerAPI.RevokeInventoryItemAsync(request);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<RevokeInventoryResult>(removeResult.Error);
            }
            return new ExecuteResult<RevokeInventoryResult>
            {
                Result = removeResult.Result
            };
        }

        public static async Task<ExecuteResult<PlayFab.ServerModels.EmptyResponse>> UpdateItemInstanceDataAsync(string profileID, string instanceID, string dataKey, string dataValue)
        {
            var dictData = new Dictionary<string, string>();
            dictData.Add(dataKey, dataValue);

            var updateRequest = new UpdateUserInventoryItemDataRequest {
                ItemInstanceId = instanceID,
                PlayFabId = profileID,
                Data = dictData
            };

            var result = await fabServerAPI.UpdateUserInventoryItemCustomDataAsync(updateRequest);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<PlayFab.ServerModels.EmptyResponse>(result.Error);
            }
            return new ExecuteResult<PlayFab.ServerModels.EmptyResponse>
            {
                Result = result.Result
            };
        }

        public static async Task AwaitIfLock(string lockKey)
        {
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await using (await locker.AcquireLockAsync(lockKey))
            {
                await using var handle = await locker.TryAcquireLockAsync(lockKey);          
            }
        }

        public static async Task CreateQueueIfNotExistAsync(string queueName)
        {
            var queueClient = new QueueClient(StorageConnectionString, queueName);
            await queueClient.CreateIfNotExistsAsync();
        }

        public static async Task CreateStorageContainerIfNotExistAsync(string containerName)
        {
            var container = new BlobContainerClient(StorageConnectionString, containerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.None);
        }

        public static async Task CreateLockContainerIfNotExistAsync()
        {
            await CreateStorageContainerIfNotExistAsync(LockContainer);
        }

        // utils
        public static long DateToTimestamp(DateTime data)
        {
            return new DateTimeOffset(data).ToUnixTimeMilliseconds();
        }

        public static DateTime TimeStampToDate(long unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddMilliseconds( unixTimeStamp ).ToUniversalTime();
            return dateTime;
        } 
    }
}