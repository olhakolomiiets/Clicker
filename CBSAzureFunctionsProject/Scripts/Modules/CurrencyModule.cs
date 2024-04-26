using PlayFab.ServerModels;
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
using System;

namespace CBS
{
    public class CurrencyModule : BaseAzureModule
    {
        private static readonly string CurrencyCatalogID = CatalogKeys.CurrencyCatalogID;

        [FunctionName(AzureFunctions.GetProfileCurrencyMethod)]
        public static async Task<dynamic> GetProfileCurrencyTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();

            var profileID = request.ProfileID;

            var getResult = await GetProfileCurrenciesAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.AddProfileCurrencyMethod)]
        public static async Task<dynamic> AddProfileCurrencyMethodTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionChangeCurrencyRequest>();

            var profileID = request.ProfileID;
            var code = request.Code;
            var amount = request.Amount;

            var updateResult = await AddVirtualCurrencyToProfileAsync(profileID, code, amount);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.SubtractProfileCurrencyMethod)]
        public static async Task<dynamic> SubtractProfileCurrencyMethodTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionChangeCurrencyRequest>();

            var profileID = request.ProfileID;
            var code = request.Code;
            var amount = request.Amount;

            var updateResult = await SubtractVirtualCurrencyFromProfileAsync(profileID, code, amount);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError(updateResult.Error).AsFunctionResult();
            }

            return updateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetCurrenciesPackMethod)]
        public static async Task<dynamic> GetCurrenciesPackTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionKeyRequest>();

            var getResult = await GetCurrenciesPacksAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GrantCurrencyPackMethod)]
        public static async Task<dynamic> GrantCurrencyPackTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionGrantItemRequest>();
            var profileID = request.ProfileID;
            var packID = request.ItemID;

            var grantResult = await GrantPackToProfileAsync(profileID, packID);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError(grantResult.Error).AsFunctionResult();
            }

            return grantResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionCurrenciesResult>> GetProfileCurrenciesAsync(string profileID)
        {
            var request = new GetUserInventoryRequest
            {
                PlayFabId = profileID
            };
            var inventoryResult = await FabServerAPI.GetUserInventoryAsync(request);
            if (inventoryResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCurrenciesResult>(inventoryResult.Error);
            }
            var currencies = ParseCurrencies(inventoryResult.Result);
            return new ExecuteResult<FunctionCurrenciesResult>
            {
                Result = new FunctionCurrenciesResult
                {
                    TargetID = profileID,
                    Currencies = currencies
                }
            };
        }

        public static async Task<ExecuteResult<FunctionChangeCurrencyResult>> AddVirtualCurrencyToProfileAsync(string profileID, string code, int value)
        {
            var request = new AddUserVirtualCurrencyRequest{
                PlayFabId = profileID,
                Amount = value,
                VirtualCurrency = code
            };

            var addResult = await FabServerAPI.AddUserVirtualCurrencyAsync(request);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChangeCurrencyResult>(addResult.Error);
            }

            var getCurrencyResult = await GetProfileCurrenciesAsync(profileID);
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
                    TargetID = profileID,
                    BalanceChange = balanceChange,
                    UpdatedCurrency = updatedCurrency,
                }
            };
        } 

        public static async Task<ExecuteResult<FunctionChangeCurrencyResult>> SubtractVirtualCurrencyFromProfileAsync(string profileID, string code, int value)
        {
            var request = new SubtractUserVirtualCurrencyRequest
            {
                PlayFabId = profileID,
                Amount = value,
                VirtualCurrency = code
            };

            var getCurrencyResult = await GetProfileCurrenciesAsync(profileID);
            if (getCurrencyResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChangeCurrencyResult>(getCurrencyResult.Error);
            }
            var currencies = getCurrencyResult.Result.Currencies;

            if (!EnoughFundsToSubtract(getCurrencyResult.Result, code, value))
            {
                return ErrorHandler.InsufficientFundsError<FunctionChangeCurrencyResult>();
            }

            var addResult = await FabServerAPI.SubtractUserVirtualCurrencyAsync(request);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionChangeCurrencyResult>(addResult.Error);
            }

            getCurrencyResult = await GetProfileCurrenciesAsync(profileID);
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
                    TargetID = profileID,
                    BalanceChange = balanceChange,
                    UpdatedCurrency = updatedCurrency,
                }
            };
        } 

        public static async Task<ExecuteResult<FunctionCatalogItemsResult>> GetCurrenciesPacksAsync()
        {
            var request = new GetCatalogItemsRequest
            {
                CatalogVersion = CurrencyCatalogID
            };
            var itemsResult = await FabServerAPI.GetCatalogItemsAsync(request);
            if (itemsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionCatalogItemsResult>(itemsResult.Error);
            }
            var fabItems = itemsResult.Result.ToClientInstance().Catalog;

            return new ExecuteResult<FunctionCatalogItemsResult>{
                Result = new FunctionCatalogItemsResult
                {
                    Items = fabItems
                }
            };
        }

        public static async Task<ExecuteResult<FunctionGrantCurrencyPackResult>> GrantPackToProfileAsync(string profileID, string packID)
        {
            var getResult = await GetCurrenciesPacksAsync();
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantCurrencyPackResult>(getResult.Error);
            }
            var packs = getResult.Result.Items;
            var packItem = packs.FirstOrDefault(x=>x.ItemId == packID);
            if (packItem == null)
            {
                return ErrorHandler.CurrencyPackNotFoundError<FunctionGrantCurrencyPackResult>();
            }
            var currenciesToAdd = packItem.Bundle?.BundledVirtualCurrencies;

            var itemsList = new List<string>() { packID };
            var grantResult = await InternalGrantItemsToPlayerAsync(CurrencyCatalogID, itemsList, profileID);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantCurrencyPackResult>(grantResult.Error);
            }
            var grantItem = grantResult.Result.ItemGrantResults.FirstOrDefault();
            var instanceID = grantItem.ItemInstanceId;

            var removeResult = await RemoveProfileInventoryItem(profileID, instanceID);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantCurrencyPackResult>(removeResult.Error);
            }

            return new ExecuteResult<FunctionGrantCurrencyPackResult>
            {
                Result = new FunctionGrantCurrencyPackResult
                {
                    ProfileID = profileID,
                    GrantedCurrencies = currenciesToAdd
                }  
            };
        }

        // internal
        public  static Dictionary<string, CBSCurrency> ParseCurrencies(GetUserInventoryResult inventory)
        {
            var cbsCurrency = new Dictionary<string, CBSCurrency>();
            var fabCurrency = inventory.VirtualCurrency ?? new Dictionary<string, int>();
            var fabRechargeCurrency = inventory.VirtualCurrencyRechargeTimes ?? new Dictionary<string, VirtualCurrencyRechargeTime>();
            var currenyCount = fabCurrency.Count;
            for (int i=0;i<currenyCount;i++)
            {
                var fabTarget = fabCurrency.ElementAt(i);
                var key = fabTarget.Key;
                var value = fabTarget.Value;
                var rechargeable = fabRechargeCurrency.ContainsKey(key);
                var maxRecharge = rechargeable ? fabRechargeCurrency[key].RechargeMax : 0;
                DateTime? rechargeDate = rechargeable ? fabRechargeCurrency[key].RechargeTime : null;
                var secondsToRecharge = rechargeable ? fabRechargeCurrency[key].SecondsToRecharge : 0;

                cbsCurrency[key] = new CBSCurrency
                {
                    Code = key,
                    Value = value,
                    Rechargeable = rechargeable,
                    MaxRecharge = maxRecharge,
                    RechargeTime = rechargeDate,
                    SecondsToRecharge = secondsToRecharge
                };
            }
            return cbsCurrency;
        }

        public static bool EnoughFundsToSubtract(FunctionCurrenciesResult currenciesResult, string code, int amount)
        {
            var currencies = currenciesResult.Currencies;
            if (!currencies.ContainsKey(code))
                return false;
            var currencyToChange = currencies[code];
            var value = currencyToChange.Value;
            return value >= amount;
        }
    }
}