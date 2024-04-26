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

namespace CBS
{
    public class InAppPurchaseModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.ValidateIAPPurchaseMethod)]
        public static async Task<dynamic> ValidateIAPPurchaseTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionFabItemRequest>();

            var getResult = await ValidatePurchaseReceiptAsync(request);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PostIAPProcessMethod)]
        public static async Task<dynamic> PostIAPProcessTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIAPPostProcessRequest>();

            var processResult = await PostPurchaseProcessAsync(request);
            if (processResult.Error != null)
            {
                return ErrorHandler.ThrowError(processResult.Error).AsFunctionResult();
            }

            return processResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionValidatePurchaseResult>> ValidatePurchaseReceiptAsync(FunctionFabItemRequest request)
        {
            var profileID = request.ProfileID;
            var itemdID = request.ItemID;
            var catalogID = request.CatalogID;

            var itemsList = new List<string> { itemdID };
            var grantResult = await InternalGrantItemsToPlayerAsync(catalogID, itemsList, profileID);
            if (grantResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionValidatePurchaseResult>(grantResult.Error);
            }
            var itemsResult = grantResult.Result.ItemGrantResults.ToClientInstances();

            return new ExecuteResult<FunctionValidatePurchaseResult>
            {
                Result = new FunctionValidatePurchaseResult
                {
                    ProfileID = profileID,
                    ItemsInstances = itemsResult
                }
            };
        }

        public static async Task<ExecuteResult<FunctionIAPPostPurchaseResult>> PostPurchaseProcessAsync(FunctionIAPPostProcessRequest request)
        {
            var profileID = request.ProfileID;
            var itemdID = request.ItemID;
            var catalogID = request.CatalogID;
            var isPack = request.IsPack;
            var storeID = request.StoreID;
            var timeZone = request.TimeZoneOffset;

            var grantedCurrencies = new Dictionary<string, uint>();
            StoreLimitationInfo limitation = null;

            if (isPack && catalogID != CatalogKeys.ItemsCatalogID)
            {
                var getInventoryResult = await InventoryModule.GetProfileInventoryFromAllCatalogsAsync(profileID);
                if (getInventoryResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionIAPPostPurchaseResult>(getInventoryResult.Error);
                }

                var inventoryItems = getInventoryResult.Result;
                var packItem = inventoryItems.FirstOrDefault(x=>x.ItemId == itemdID);
                
                if (packItem != null)
                {
                    var itemInstanceID = packItem.ItemInstanceId;
                    var removeResult = await RemoveProfileInventoryItem(profileID, itemInstanceID);
                    if (removeResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionIAPPostPurchaseResult>(removeResult.Error);
                    }

                    var itemsResult = await FabServerAPI.GetCatalogItemsAsync(new GetCatalogItemsRequest
                    {
                        CatalogVersion = catalogID
                    });
                    if (itemsResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionIAPPostPurchaseResult>(itemsResult.Error);
                    }
                    var fabItems = itemsResult.Result.Catalog;
                    var fabItem = fabItems.FirstOrDefault(x=>x.ItemId == itemdID);
                    var itemBundle = fabItem.Bundle ?? new CatalogItemBundleInfo();
                    grantedCurrencies = itemBundle.BundledVirtualCurrencies;
                }
            }

            if (catalogID == CatalogKeys.ItemsCatalogID)
            {
                if (string.IsNullOrEmpty(storeID) && isPack)
                {
                    var postResult = await ItemsModule.PostPurchaseProccessAsync(profileID, itemdID);
                    if (postResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionIAPPostPurchaseResult>(postResult.Error);
                    }
                    var result = postResult.Result;
                    grantedCurrencies = result.PurchasedCurrencies;
                }

                if (!string.IsNullOrEmpty(storeID))
                {
                    var postRequest = new FunctionValidateStorePurchaseRequest
                    {
                        IsPack = isPack,
                        ProfileID = profileID,
                        ItemID = itemdID,
                        StoreID = storeID,
                        TimeZoneOffset = timeZone
                    };
                    var postResult = await StoreModule.PostStorePurchaseProcessAsync(postRequest);
                    if (postResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionIAPPostPurchaseResult>(postResult.Error);
                    }
                    var result = postResult.Result;
                    grantedCurrencies = result.PurchasedCurrencies;
                    limitation = result.Limitation;
                }
            }

            return new ExecuteResult<FunctionIAPPostPurchaseResult>
            {
                Result = new FunctionIAPPostPurchaseResult
                {
                    ProfileID = profileID,
                    GrantedCurrencies = grantedCurrencies,
                    LimitationInfo = limitation
                }
            };
        }
    }
}