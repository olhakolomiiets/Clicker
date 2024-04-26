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
using Medallion.Threading.Azure;
using Medallion.Threading;
using System.Linq;
using PlayFab;

namespace CBS
{
    public class ClanEconomyModule : BaseAzureModule
    {
        private static readonly string LockIDPrefix = "claneconomy"; 
        private static readonly string ItemsCatalogID = CatalogKeys.ItemsCatalogID;

        [FunctionName(AzureFunctions.GetClanInventoryMethod)]
        public static async Task<dynamic> GetClanInventoryTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var clanID = request.ID;

            var getResult = await GetClanInventoryAsync(clanID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }
            var inventory = getResult.Result;
            return new FunctionGetInventoryResult
            {
                Instances = inventory.ToClientInstances()
            }.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GrantItemsToClanMethod)]
        public static async Task<dynamic> GrantItemsToClanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<GrantItemsToClanRequest>();

            var grantResult = await GrantItemsToClanAsync(request);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError(grantResult.Error).AsFunctionResult();
            }

            return grantResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetClanCurrencyMethod)]
        public static async Task<dynamic> GetClanCurrencyTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();

            var profileID = request.ProfileID;
            var clanID = request.ID;

            var getResult = await GetClanCurrenciesAsync(clanID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddClanCurrencyMethod)]
        public static async Task<dynamic> AddClanCurrencyTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionChangeClanCurrencyRequest>();

            var profileID = request.ProfileID;
            var code = request.Code;
            var amount = request.Amount;
            var clanID = request.ClanID;

            var updateResult = await AddVirtualCurrencyToClanAsync(clanID, code, amount);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SubtractClanCurrencyMethod)]
        public static async Task<dynamic> SubtractClanCurrencyTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionChangeClanCurrencyRequest>();

            var profileID = request.ProfileID;
            var code = request.Code;
            var amount = request.Amount;
            var clanID = request.ClanID;

            var updateResult = await SubtractVirtualCurrencyFromClanAsync(clanID, code, amount);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.TransferItemFromProfileToClanMethod)]
        public static async Task<dynamic> TransferItemFromProfileToClanTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanTransferItemRequest>();

            var transferResult = await TransferItemFromProfileToClanAsync(request);
            if (transferResult.Error != null)
            {
                return ErrorHandler.ThrowError(transferResult.Error).AsFunctionResult();
            }

            return transferResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.TransferItemFromClanToProfileMethod)]
        public static async Task<dynamic> TransferItemFromClanToProfileTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionClanTransferItemRequest>();

            var transferResult = await TransferItemFromClanToProfileAsync(request);
            if (transferResult.Error != null)
            {
                return ErrorHandler.ThrowError(transferResult.Error).AsFunctionResult();
            }

            return transferResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<List<ItemInstance>>> GetClanInventoryAsync(string clanID)
        {
            var request = new GetUserInventoryRequest
            {
                PlayFabId = clanID
            };
            var result = await FabServerAPI.GetUserInventoryAsync(request);
            if (result.Error != null)
            {
                return ErrorHandler.ThrowError<List<ItemInstance>>(result.Error);
            }
            var inventory = result.Result.Inventory ?? new List<ItemInstance>();
            var catalogInventory = inventory.Where(x=>x.CatalogVersion == CatalogKeys.ItemsCatalogID).ToList();
            return new ExecuteResult<List<ItemInstance>>
            {
                Result = catalogInventory
            };
        }

        public static async Task<ExecuteResult<FunctionGrantItemsResult>> GrantItemsToClanAsync(GrantItemsToClanRequest request)
        {
            var clanID = request.ClanID;
            var itemsIDs = request.ItemsIDs;
            var containPack = request.ContainPack;

            // distributed lock by entity id
            var lockID = LockIDPrefix + clanID;
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(lockID))
            {
                await using var handle = await locker.TryAcquireLockAsync(lockID);  
                var checkClanResult = await ClanModule.CheckIfProfileIsClanAsync(clanID);
                if (checkClanResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGrantItemsResult>(checkClanResult.Error);
                }
                var isClanProfile = checkClanResult.Result.Value;
                if (!isClanProfile)
                {
                    return ErrorHandler.InvalidInput<FunctionGrantItemsResult>();
                }

                var grantedCurrencies = new Dictionary<string, uint>();

                var grantRequest = new GrantItemsToUserRequest
                {
                    CatalogVersion = ItemsCatalogID,
                    ItemIds = itemsIDs.ToList(),
                    PlayFabId = clanID
                };
                var grantResult = await FabServerAPI.GrantItemsToUserAsync(grantRequest);
                if (grantResult.Error != null)
                {
                    ErrorHandler.ThrowError<FunctionGrantItemsResult>(grantResult.Error);
                }
                var fabInstances = grantResult.Result.ItemGrantResults;

                if (containPack)
                {
                    var inventoryBundlesItems = fabInstances;
                    var catalogItemsIDs = inventoryBundlesItems.Select(x=>x.ItemId).ToList();
                    var inventoryIds = inventoryBundlesItems.Select(x=>x.ItemInstanceId);

                    var itemsResult = await FabServerAPI.GetCatalogItemsAsync(new GetCatalogItemsRequest
                    {
                        CatalogVersion = ItemsCatalogID
                    });
                    if (itemsResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionGrantItemsResult>(itemsResult.Error);
                    }
                    var fabItems = itemsResult.Result.Catalog;
                    var fabBundledItems = fabItems.Where(x=>catalogItemsIDs.Contains(x.ItemId) && x.Bundle != null && x.Bundle.BundledVirtualCurrencies != null);
                    var fabBundledItemsIDs = fabBundledItems.Select(x=>x.ItemId);
                    var bundlesInstances = inventoryBundlesItems.Where(x=>fabBundledItemsIDs.Contains(x.ItemId)).Select(x=>x.ItemInstanceId).ToArray();
                    fabInstances = fabInstances.Where(x=>!bundlesInstances.Contains(x.ItemInstanceId)).ToList();

                    foreach (var bundledItem in fabBundledItems)
                    {
                        grantedCurrencies = grantedCurrencies.Concat(bundledItem.Bundle.BundledVirtualCurrencies).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => (uint)x.Sum(y=>y.Value));
                    }
                    var revokeResult = await RevokeInventoryItemsFromClanAsync(clanID, bundlesInstances);
                    if (revokeResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionGrantItemsResult>(revokeResult.Error);
                    }
                }

                return new ExecuteResult<FunctionGrantItemsResult>
                {
                    Result = new FunctionGrantItemsResult
                    {
                        TargetID = clanID,
                        GrantedInstances = fabInstances.ToClientInstances(),
                        GrantedCurrencies = grantedCurrencies
                    }
                };
            }
        }

        public static async Task<ExecuteResult<FunctionRevokeInventoryItemsResult>> RevokeInventoryItemsFromClanAsync(string clanID, string[] instanceIds)
        {
            var revokeList = instanceIds.Select(x=> new RevokeInventoryItem
            {
                PlayFabId = clanID,
                ItemInstanceId = x
            }).ToList();
            var revokeRequest = new RevokeInventoryItemsRequest
            {
                Items = revokeList
            };
            var revokeResult = await FabServerAPI.RevokeInventoryItemsAsync(revokeRequest);
            if (revokeResult.Error != null)
            {
                ErrorHandler.ThrowError<FunctionRevokeInventoryItemsResult>(revokeResult.Error);
            }
            var errorList = revokeResult.Result.Errors;
            var errorIDs = errorList.Select(x=>x.Item.ItemInstanceId);
            var successList = instanceIds.Where(x=>!errorIDs.Contains(x)).ToArray();

            return new ExecuteResult<FunctionRevokeInventoryItemsResult>
            {
                Result = new FunctionRevokeInventoryItemsResult
                {
                    TargetID = clanID,
                    RevomedInstanceIDs = successList
                }
            };
        }

        public static async Task<ExecuteResult<FunctionCurrenciesResult>> GetClanCurrenciesAsync(string clanID)
        {
            var request = new GetUserInventoryRequest
            {
                PlayFabId = clanID
            };
            var inventoryResult = await FabServerAPI.GetUserInventoryAsync(request);
            if (inventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCurrenciesResult>(inventoryResult.Error);
            }
            var currencies = CurrencyModule.ParseCurrencies(inventoryResult.Result);
            return new ExecuteResult<FunctionCurrenciesResult>
            {
                Result = new FunctionCurrenciesResult
                {
                    TargetID = clanID,
                    Currencies = currencies
                }
            };
        }

        public static async Task<ExecuteResult<FunctionChangeCurrencyResult>> AddVirtualCurrencyToClanAsync(string clanID, string code, int value)
        {
            // distributed lock by entity 
            var lockID = LockIDPrefix + clanID;
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(lockID))
            {
                await using var handle = await locker.TryAcquireLockAsync(lockID);
                var request = new AddUserVirtualCurrencyRequest{
                    PlayFabId = clanID,
                    Amount = value,
                    VirtualCurrency = code
                };

                var addResult = await FabServerAPI.AddUserVirtualCurrencyAsync(request);
                if (addResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionChangeCurrencyResult>(addResult.Error);
                }

                var getCurrencyResult = await GetClanCurrenciesAsync(clanID);
                if (getCurrencyResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionChangeCurrencyResult>(getCurrencyResult.Error);
                }

                var currencies = getCurrencyResult.Result.Currencies;
                var updatedCurrency = currencies[code];
                var balanceChange = addResult.Result.BalanceChange;

                return new ExecuteResult<FunctionChangeCurrencyResult>
                {
                    Result = new FunctionChangeCurrencyResult
                    {
                        TargetID = clanID,
                        BalanceChange = balanceChange,
                        UpdatedCurrency = updatedCurrency,
                    }
                };
            }

            
        } 

        public static async Task<ExecuteResult<FunctionChangeCurrencyResult>> SubtractVirtualCurrencyFromClanAsync(string clanID, string code, int value)
        {
            // distributed lock by entity id
            var lockID = LockIDPrefix + clanID;
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(lockID))
            {
                await using var handle = await locker.TryAcquireLockAsync(lockID);  
                var request = new SubtractUserVirtualCurrencyRequest
                {
                    PlayFabId = clanID,
                    Amount = value,
                    VirtualCurrency = code
                };

                var getCurrencyResult = await GetClanCurrenciesAsync(clanID);
                if (getCurrencyResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionChangeCurrencyResult>(getCurrencyResult.Error);
                }
                var currencies = getCurrencyResult.Result.Currencies;

                if (!CurrencyModule.EnoughFundsToSubtract(getCurrencyResult.Result, code, value))
                {
                    return ErrorHandler.InsufficientFundsError<FunctionChangeCurrencyResult>();
                }

                var addResult = await FabServerAPI.SubtractUserVirtualCurrencyAsync(request);
                if (addResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionChangeCurrencyResult>(addResult.Error);
                }

                getCurrencyResult = await GetClanCurrenciesAsync(clanID);
                if (getCurrencyResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionChangeCurrencyResult>(getCurrencyResult.Error);
                }

                currencies = getCurrencyResult.Result.Currencies;
                var updatedCurrency = currencies[code];
                var balanceChange = addResult.Result.BalanceChange;

                return new ExecuteResult<FunctionChangeCurrencyResult>
                {
                    Result = new FunctionChangeCurrencyResult
                    {
                        TargetID = clanID,
                        BalanceChange = balanceChange,
                        UpdatedCurrency = updatedCurrency,
                    }
                };
            }
        }

        public static async Task<ExecuteResult<FunctionClanTransferItemResult>> TransferItemFromProfileToClanAsync(FunctionClanTransferItemRequest request)
        {
            var profileID = request.ProfileID;
            var profileAuthContext = request.ProfileAuthContext;
            var itemInstanceId = request.ItemInstanceID;
            var clanID = request.ClanID;

            // distributed lock by entity id
            var lockID = LockIDPrefix + clanID;
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(lockID))
            {
                await using var handle = await locker.TryAcquireLockAsync(lockID);  
                if (!string.IsNullOrEmpty(profileID))
                {
                    var hasPermissionResult = await ClanModule.HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.PUT_TO_CLAN_INVENTORY);
                    if (hasPermissionResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionClanTransferItemResult>(hasPermissionResult.Error);
                    }
                    var hasPermission = hasPermissionResult.Result.Value;
                    if (!hasPermission)
                    {
                        return ErrorHandler.NotEnoughRights<FunctionClanTransferItemResult>();
                    }
                }

                var openTradeRequest = new PlayFab.ClientModels.OpenTradeRequest
                {
                    AuthenticationContext = profileAuthContext,
                    OfferedInventoryInstanceIds = new List<string>() { itemInstanceId }
                };
                var openResult = await FabClientAPI.OpenTradeAsync(openTradeRequest);
                if (openResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTransferItemResult>(openResult.Error);
                }
                var tradeInfo = openResult.Result.Trade;
                var tradeID = tradeInfo.TradeId;

                var getClanAuthIDResult = await ClanModule.GetClanAuthIDAsync(clanID);
                if (getClanAuthIDResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTransferItemResult>(getClanAuthIDResult.Error);
                }
                var authID = getClanAuthIDResult.Result;
                if (string.IsNullOrEmpty(authID))
                {
                    return ErrorHandler.InvalidInput<FunctionClanTransferItemResult>();
                }
                
                PlayFabSettings.staticSettings.TitleId = TitleId;
                PlayFabSettings.staticSettings.DeveloperSecretKey = SercetKey;
                var loginRequest = new PlayFab.ClientModels.LoginWithCustomIDRequest
                {
                    CustomId = authID
                };
                var loginResult = await FabClientAPI.LoginWithCustomIDAsync(loginRequest);
                if (loginResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTransferItemResult>(loginResult.Error);
                }
                var clanAuthContext = loginResult.Result.AuthenticationContext;

                var acceptRequest = new PlayFab.ClientModels.AcceptTradeRequest
                {
                    OfferingPlayerId = profileID,
                    TradeId = tradeID,
                    AuthenticationContext = clanAuthContext
                };
                var acceptResult = await FabClientAPI.AcceptTradeAsync(acceptRequest);
                if (acceptResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTransferItemResult>(acceptResult.Error);
                }
                
                return new ExecuteResult<FunctionClanTransferItemResult>
                {
                    Result = new FunctionClanTransferItemResult
                    {
                        ProfileID = profileID,
                        ClanID = clanID,
                        ItemInstanceID = itemInstanceId,
                        TransferID = tradeID
                    }
                };
            }           
        } 

        public static async Task<ExecuteResult<FunctionClanTransferItemResult>> TransferItemFromClanToProfileAsync(FunctionClanTransferItemRequest request)
        {
            var profileID = request.ProfileID;
            var profileAuthContext = request.ProfileAuthContext;
            var itemInstanceId = request.ItemInstanceID;
            var clanID = request.ClanID;

            // distributed lock by entity id
            var lockID = LockIDPrefix + clanID;
            var locker = new AzureBlobLeaseDistributedSynchronizationProvider(BlobContainer);
            await CreateLockContainerIfNotExistAsync();
            await using (await locker.AcquireLockAsync(lockID))
            {
                await using var handle = await locker.TryAcquireLockAsync(lockID);  
                if (!string.IsNullOrEmpty(profileID))
                {
                    var hasPermissionResult = await ClanModule.HasPermissionForActionAsync(clanID, profileID, ClanRolePermission.CLAIM_CLAN_INVENTORY_ITEM);
                    if (hasPermissionResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionClanTransferItemResult>(hasPermissionResult.Error);
                    }
                    var hasPermission = hasPermissionResult.Result.Value;
                    if (!hasPermission)
                    {
                        return ErrorHandler.NotEnoughRights<FunctionClanTransferItemResult>();
                    }
                }

                var getClanAuthIDResult = await ClanModule.GetClanAuthIDAsync(clanID);
                if (getClanAuthIDResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTransferItemResult>(getClanAuthIDResult.Error);
                }
                var authID = getClanAuthIDResult.Result;
                if (string.IsNullOrEmpty(authID))
                {
                    return ErrorHandler.InvalidInput<FunctionClanTransferItemResult>();
                }

                PlayFabSettings.staticSettings.TitleId = TitleId;
                PlayFabSettings.staticSettings.DeveloperSecretKey = SercetKey;
                var loginRequest = new PlayFab.ClientModels.LoginWithCustomIDRequest
                {
                    CustomId = authID
                };
                var loginResult = await FabClientAPI.LoginWithCustomIDAsync(loginRequest);
                if (loginResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTransferItemResult>(loginResult.Error);
                }
                var clanAuthContext = loginResult.Result.AuthenticationContext;

                var openTradeRequest = new PlayFab.ClientModels.OpenTradeRequest
                {
                    AuthenticationContext = clanAuthContext,
                    OfferedInventoryInstanceIds = new List<string>() { itemInstanceId }
                };
                var openResult = await FabClientAPI.OpenTradeAsync(openTradeRequest);
                if (openResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTransferItemResult>(openResult.Error);
                }
                var tradeInfo = openResult.Result.Trade;
                var tradeID = tradeInfo.TradeId;

                var acceptRequest = new PlayFab.ClientModels.AcceptTradeRequest
                {
                    OfferingPlayerId = clanID,
                    TradeId = tradeID,
                    AuthenticationContext = profileAuthContext
                };
                var acceptResult = await FabClientAPI.AcceptTradeAsync(acceptRequest);
                if (acceptResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionClanTransferItemResult>(acceptResult.Error);
                }
                
                return new ExecuteResult<FunctionClanTransferItemResult>
                {
                    Result = new FunctionClanTransferItemResult
                    {
                        ProfileID = profileID,
                        ClanID = clanID,
                        ItemInstanceID = itemInstanceId,
                        TransferID = tradeID
                    }
                };
            }         
        } 
    }
}