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
using CBS.Utils;
using System;
using PlayFab.AdminModels;

namespace CBS
{
    public class StoreModule : BaseAzureModule
    {
        private static readonly string LimitationTableID = "CBSStoreLimitation";

        [FunctionName(AzureFunctions.GetAllStoresMethod)]
        public static async Task<dynamic> GetAllStoresTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionsGetStoresRequest>();
            var profileID = request.ProfileID;
            var timeZone = request.TimeZoneOffset;

            var getResult = await GetStoresAsync(profileID, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetAllStoreTitlesMethod)]
        public static async Task<dynamic> GetAllStoreTitlesTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionsGetStoresRequest>();
            var profileID = request.ProfileID;
            var timeZone = request.TimeZoneOffset;

            var getResult = await GetStoreTitlesAsync(profileID, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetStoreByIDMethod)]
        public static async Task<dynamic> GetStoreByIDTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionsGetStoreRequest>();
            var profileID = request.ProfileID;
            var storeID = request.StoreID;
            var timeZone = request.TimeZoneOffset;

            var getResult = await GetStoreByIDAsync(profileID, storeID, timeZone);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetStoreItemByIDMethod)]
        public static async Task<dynamic> GetStoreItemByIDTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionStoreItemRequest>();
            var profileID = request.ProfileID;
            var storeID = request.StoreID;
            var itemID = request.ItemID;
            var timeZone = request.TimeZoneOffset;

            var getResult = await GetStoreItemByIDAsync(profileID, storeID, itemID, timeZone, log);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PostStorePurchaseProccessMethod)]
        public static async Task<dynamic> PostStorePurchaseProccessTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionValidateStorePurchaseRequest>();

            var validateResult = await PostStorePurchaseProcessAsync(request);
            if (validateResult.Error != null)
            {
                return ErrorHandler.ThrowError(validateResult.Error).AsFunctionResult();
            }

            return validateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.PreStorePurchaseProccessMethod)]
        public static async Task<dynamic> PreStorePurchaseProccessTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionStoreItemRequest>();
            var profileID = request.ProfileID;
            var storeID = request.StoreID;
            var itemID = request.ItemID;
            var timeZone = request.TimeZoneOffset;

            var validateResult = await PreStorePurchaseProcessAsync(profileID, storeID, itemID, timeZone);
            if (validateResult.Error != null)
            {
                return ErrorHandler.ThrowError(validateResult.Error).AsFunctionResult();
            }

            return validateResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.RevokeStoreItemLimitationMethod)]
        public static async Task<dynamic> RevokeStoreItemLimitationTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionStoreItemRequest>();
            var profileID = request.ProfileID;
            var storeID = request.StoreID;
            var itemID = request.ItemID;

            var revokeResult = await RevokeItemLimitationAsync(profileID, storeID, itemID);
            if (revokeResult.Error != null)
            {
                return ErrorHandler.ThrowError(revokeResult.Error).AsFunctionResult();
            }

            return revokeResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<StoresContainer>> GetStoreContainerAsync()
        {
            var getResult = await GetInternalTitleDataAsObjectAsync<StoresContainer>(TitleKeys.StoreKey);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError<StoresContainer>(getResult.Error);
            } 
            var storeContainer = getResult.Result;
            return new ExecuteResult<StoresContainer>
            {
                Result = storeContainer
            };
        }

        public static async Task<ExecuteResult<FunctionGetStoreTitlesResult>> GetStoreTitlesAsync(string profileID, int timeZone)
        {
            // get profile detail
            var constraints = new CBSProfileConstraints
            {
                LoadClan = true,
                LoadLevel = true,
                LoadStatistics = true,
            };
            var profileResult = await TableProfileAssistant.GetProfileDetailAsync(profileID, constraints);
            if (profileResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetStoreTitlesResult>(profileResult.Error);
            }
            var profileDetail = profileResult.Result;

            // get all stores ids
            var getIDsResult = await GetStoreContainerAsync();
            if (getIDsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetStoreTitlesResult>(getIDsResult.Error);
            } 
            var storeContainer = getIDsResult.Result;
            var ids = storeContainer.GetStoreIDs();

            // get stores
            var requestTime = ServerTimeUTC;
            requestTime.AddMilliseconds(timeZone);
            var taskList = new List<Task<ExecuteResult<StoreTitleExecuteResult>>>();
            foreach(var id in ids)
            {
                var storeTask = GetStoreTitleWithProfileLimitsAsync(profileDetail, id, requestTime);
                taskList.Add(storeTask);
            }
            var storeResult = await Task.WhenAll(taskList);
            var storeTitles = storeResult.Where(x=>x.Error == null).Select(x=>x.Result.StoreTitle).ToList();

            return new ExecuteResult<FunctionGetStoreTitlesResult>
            {
                Result = new FunctionGetStoreTitlesResult
                {
                    StoreTitles = storeTitles
                }  
            };
        }

        public static async Task<ExecuteResult<FunctionGetStoresResult>> GetStoresAsync(string profileID, int timeZone)
        {
            // get profile detail
            var constraints = new CBSProfileConstraints
            {
                LoadClan = true,
                LoadLevel = true,
                LoadStatistics = true,
            };
            var profileResult = await TableProfileAssistant.GetProfileDetailAsync(profileID, constraints);
            var profileDetail = profileResult.Error == null ? profileResult.Result : new ProfileEntity{ProfileID = profileID};

            // get all stores ids
            var getIDsResult = await GetStoreContainerAsync();
            if (getIDsResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetStoresResult>(getIDsResult.Error);
            } 
            var storeContainer = getIDsResult.Result ?? new StoresContainer();
            var ids = storeContainer.GetStoreIDs();

            // get stores
            var requestTime = ServerTimeUTC;
            requestTime = requestTime.AddMilliseconds(timeZone);
            var taskList = new List<Task<ExecuteResult<CBSStore>>>();
            foreach(var id in ids)
            {
                var storeTask = GetStoreWithProfileLimitsAsync(profileDetail, id, timeZone);
                taskList.Add(storeTask);
            }
            var stores = new List<CBSStore>();
            if (ids.Count > 0)
            {
                var storeResult = await Task.WhenAll(taskList);
                stores = storeResult.Where(x=>x.Error == null).Select(x=>x.Result).ToList();
            }

            return new ExecuteResult<FunctionGetStoresResult>
            {
                Result = new FunctionGetStoresResult
                {
                    Stores = stores
                }  
            };
        }

        public static async Task<ExecuteResult<CBSStore>> GetStoreByIDAsync(string profileID, string storeID, int timeZone)
        {
            // get profile detail
            var constraints = new CBSProfileConstraints
            {
                LoadClan = true,
                LoadLevel = true,
                LoadStatistics = true,
            };
            var profileResult = await TableProfileAssistant.GetProfileDetailAsync(profileID, constraints);
            if (profileResult.Error != null)
            {
                return ErrorHandler.ThrowError<CBSStore>(profileResult.Error);
            }
            var profileDetail = profileResult.Result;

            // get store
            var requestTime = ServerTimeUTC;
            requestTime.AddMilliseconds(timeZone);
            var storeResult = await GetStoreWithProfileLimitsAsync(profileDetail, storeID, timeZone);
            if (storeResult.Error != null)
            {
                return ErrorHandler.ThrowError<CBSStore>(storeResult.Error);
            }
            var store = storeResult.Result;

            return new ExecuteResult<CBSStore>
            {
                Result = store
            };
        }

        public static async Task<ExecuteResult<CBSStoreItem>> GetStoreItemByIDAsync(string profileID, string storeID, string itemID, int timeZone, ILogger log)
        {
            var storeResult = await GetStoreByIDAsync(profileID, storeID, timeZone);
            if (storeResult.Error != null)
            {
                return ErrorHandler.ThrowError<CBSStoreItem>(storeResult.Error);
            }
            var store = storeResult.Result;
            var items = store.Items;
            var item = items.FirstOrDefault(x=>x.ItemID == itemID);
            if (item == null)
            {
                return ErrorHandler.StoreItemNotFound<CBSStoreItem>();
            }

            return new ExecuteResult<CBSStoreItem>
            {
                Result = item
            };
        }

        public static async Task<ExecuteResult<StoreTitleExecuteResult>> GetStoreTitleWithProfileLimitsAsync(ProfileEntity profileDetail, string storeID, DateTime requestDate)
        {
            var profileID = profileDetail.ProfileID;
            var levelInfo = profileDetail.Level;
            var level = levelInfo.Level.GetValueOrDefault();
            var clanID = profileDetail.ClanID;
            var statistics = profileDetail.Statistics;

            var storeRequest = new GetStoreItemsServerRequest
            {
                PlayFabId = profileID,
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = storeID
            };
            var getStoreResult = await FabServerAPI.GetStoreItemsAsync(storeRequest);
            if (getStoreResult.Error != null)
            {
                return ErrorHandler.ThrowError<StoreTitleExecuteResult>(getStoreResult.Error);
            }
            var store = getStoreResult.Result;
            var storeName = store.MarketingData.DisplayName;
            var storeDescription = store.MarketingData.Description;
            var storeRawObject = store.MarketingData.Metadata;
            var storeRawData = storeRawObject == null ? JsonPlugin.EMPTY_JSON : storeRawObject.ToString();
            var storeMeta = new CBSStoreMeta();
            try
            {
                storeMeta = JsonPlugin.FromJsonDecompress<CBSStoreMeta>(storeRawData);
            }
            catch
            {
                storeMeta = JsonPlugin.FromJson<CBSStoreMeta>(storeRawData);
            }

            // check enabled
            var storeEnabled = storeMeta.Enable;
            if (!storeEnabled)
            {
                return ErrorHandler.StoreNotEnabled<StoreTitleExecuteResult>();
            }

            // check level
            var hasLevelLimit = storeMeta.HasLevelLimit;
            if (hasLevelLimit)
            {
                var levelLimit = storeMeta.LevelLimit;
                var limitFilter = storeMeta.LevelFilter;
                if (limitFilter == IntFilter.EQUAL)
                {
                    if (levelLimit != level)
                    {
                        return ErrorHandler.StoreNotEnabled<StoreTitleExecuteResult>();
                    }
                }
                else if (limitFilter == IntFilter.EQUAL_OR_GREATER)
                {
                    if (level < levelLimit)
                    {
                        return ErrorHandler.StoreNotEnabled<StoreTitleExecuteResult>();
                    }
                }
                else if (limitFilter == IntFilter.EQUAL_OR_LESS)
                {
                    if (level > levelLimit)
                    {
                        return ErrorHandler.StoreNotEnabled<StoreTitleExecuteResult>();
                    }
                }
            }

            // check statistics
            var hasStatisticsLimit = storeMeta.HasStatisticLimit;
            if (hasStatisticsLimit)
            {
                if (statistics == null || statistics.Statistics == null)
                {
                    return ErrorHandler.StoreNotEnabled<StoreTitleExecuteResult>();
                }
                var statisticsName = storeMeta.StatisticLimitName;
                var statisticsValue = storeMeta.StatisticLimitValue;
                var limitFilter = storeMeta.StatisticFilter;
                var statisitcsDict = statistics.Statistics;
                if (!statisitcsDict.ContainsKey(statisticsName))
                {
                    return ErrorHandler.StoreNotEnabled<StoreTitleExecuteResult>();
                }
                var profileValue = statisitcsDict[statisticsName].Value;
                if (limitFilter == IntFilter.EQUAL)
                {
                    if (statisticsValue != profileValue)
                    {
                        return ErrorHandler.StoreNotEnabled<StoreTitleExecuteResult>();
                    }
                }
                else if (limitFilter == IntFilter.EQUAL_OR_GREATER)
                {
                    if (profileValue < statisticsValue)
                    {
                        return ErrorHandler.StoreNotEnabled<StoreTitleExecuteResult>();
                    }
                }
                else if (limitFilter == IntFilter.EQUAL_OR_LESS)
                {
                    if (profileValue > statisticsValue)
                    {
                        return ErrorHandler.StoreNotEnabled<StoreTitleExecuteResult>();
                    }
                }
            }

            // check clan
            var hasClanLimit = storeMeta.HasClanLimit;
            if (hasClanLimit)
            {
                if (string.IsNullOrEmpty(clanID))
                {
                    return ErrorHandler.StoreNotEnabled<StoreTitleExecuteResult>();
                }
            }

            // parse store
            var cbsStoreTitle = new CBSStoreTitle();
            cbsStoreTitle.ID = storeID;
            cbsStoreTitle.DisplayName = storeName;
            cbsStoreTitle.Description = storeDescription;
            cbsStoreTitle.CustomDataClassName = storeMeta.CustomDataClassName;
            cbsStoreTitle.CustomRawData = storeMeta.CustomRawData;

            return new ExecuteResult<StoreTitleExecuteResult>
            {
                Result = new StoreTitleExecuteResult
                {
                    StoreTitle = cbsStoreTitle,
                    OriginStore = store
                }
            };
        }

        public static async Task<ExecuteResult<CBSStore>> GetStoreWithProfileLimitsAsync(ProfileEntity profileDetail, string storeID, int timeZone)
        {
            var profileID = profileDetail.ProfileID;
            var requestDate = ServerTimeUTC.AddMilliseconds(timeZone);

            var storeTitleResult = await GetStoreTitleWithProfileLimitsAsync(profileDetail, storeID, requestDate);
            if (storeTitleResult.Error != null)
            {
                return ErrorHandler.ThrowError<CBSStore>(storeTitleResult.Error);
            }
            var titleResult = storeTitleResult.Result;
            var storeTitle = titleResult.StoreTitle;
            var originStore = titleResult.OriginStore;

            // parse store
            var cbsStore = new CBSStore();
            cbsStore.ID = storeID;
            cbsStore.DisplayName = storeTitle.DisplayName;
            cbsStore.Description = storeTitle.Description;
            cbsStore.CustomDataClassName = storeTitle.CustomDataClassName;
            cbsStore.CustomRawData = storeTitle.CustomRawData;

            // parse items
            var fabItems = originStore.Store ?? new List<PlayFab.ServerModels.StoreItem>();
            fabItems = fabItems.OrderBy(x=>x.DisplayPosition).ToList();
            var cbsItems = new List<CBSStoreItem>();
            var limitTasks = new List<Task<ExecuteResult<StoreLimitationInfo>>>();
            foreach (var fabItem in fabItems)
            {
                var itemID = fabItem.ItemId;
                var prices = fabItem.VirtualCurrencyPrices;
                var dataObject = fabItem.CustomData;
                var itemRawData = dataObject == null ? JsonPlugin.EMPTY_JSON : dataObject.ToString();
                var itemMeta = new CBSStoreItemMeta();
                try
                {
                    itemMeta = JsonPlugin.FromJsonDecompress<CBSStoreItemMeta>(itemRawData);
                }
                catch
                {
                    itemMeta = JsonPlugin.FromJson<CBSStoreItemMeta>(itemRawData);
                }
                var itemEnabled = itemMeta.Enable;
                if (itemEnabled)
                {
                    var itemDisplayName = itemMeta.SlotDisplayName;
                    var itemDescription = itemMeta.Description;
                    var hasQuantityLimit = itemMeta.HasQuantityLimit;
                    var discounts = itemMeta.Discounts;
                    var cbsItem = new CBSStoreItem
                    {
                        ItemID = itemID,
                        DisplayName = itemDisplayName,
                        Description = itemDescription,
                        Prices = fabItem.VirtualCurrencyPrices,
                        StoreID = storeID,
                        HasQuantityLimit = hasQuantityLimit,
                        Discounts = discounts,
                        CustomDataClassName = itemMeta.CustomDataClassName,
                        CustomRawData = itemMeta.CustomRawData
                    };
                    if (hasQuantityLimit)
                    {
                        limitTasks.Add(GetStoreItemLimitationAsync(cbsItem, profileID, itemMeta, timeZone));
                    }
                    cbsItems.Add(cbsItem);
                }
            }
            if (limitTasks.Count > 0)
            {
                await Task.WhenAll(limitTasks);
            }
            cbsStore.Items = cbsItems;

            return new ExecuteResult<CBSStore>
            {
                Result = cbsStore
            };
        }

        public static async Task<ExecuteResult<StoreLimitationInfo>> GetStoreItemLimitationAsync(CBSStoreItem storeItem, string profileID, CBSStoreItemMeta meta, int timeZone)
        {
            var hasQuantityLimit = meta.HasQuantityLimit;
            if (!hasQuantityLimit)
                return null;
            var storeID = storeItem.StoreID;
            var itemID = storeItem.ItemID;
            var period = meta.QuanityLimitPeriod;
            var quantityLimit = meta.QuantityLimit;
            var requestDate = ServerTimeUTC.AddMilliseconds(timeZone);

            // generate period id
            var periodID = StoreUtils.GetStoreItemID(storeID, itemID);

            // get period state
            var periodStateResult = await TimePeriodAssistant.GetPeriodStateAsync(LimitationTableID, profileID, periodID, period, requestDate);
            if (periodStateResult.Error != null)
            {
                return ErrorHandler.ThrowError<StoreLimitationInfo>(periodStateResult.Error);
            }
            var periodState = periodStateResult.Result;
            var limitationInfo = new StoreLimitationInfo();
            limitationInfo.LimitPeriod = period;
            limitationInfo.MaxQuantity = quantityLimit;
            limitationInfo.ResetLimitDate = periodState.NextCheckIn;
            if (periodState.CheckinAvailable)
            {
                limitationInfo.LeftQuantity = quantityLimit;
                var checkInResult = await TimePeriodAssistant.CheckIn(LimitationTableID, profileID, periodID, requestDate, period, quantityLimit.ToString());
                if (checkInResult.Error != null)
                {
                    return ErrorHandler.ThrowError<StoreLimitationInfo>(checkInResult.Error);
                }
                var checkInInfo = checkInResult.Result;
                limitationInfo.ResetLimitDate = checkInInfo.DateOfNextCheckIn;
            }
            else
            {  
                var rawData = periodState.RawData;
                var leftCount = quantityLimit;
                int.TryParse(rawData, out leftCount);
                limitationInfo.LeftQuantity = leftCount;
            }

            storeItem.Limitation = limitationInfo;

            return new ExecuteResult<StoreLimitationInfo>
            {
                Result = limitationInfo
            };
        }

        public static async Task<ExecuteResult<FunctionPreStorePurchaseResult>> PreStorePurchaseProcessAsync(string profileID, string storeID, string itemID, int timeZone)
        {
            // check special offer
            var isSpecialOffer = IsSpecialOffer(storeID);
            if (isSpecialOffer)
            {
                var checkOfferResult = await SpecialOfferModule.PreSpecialOfferPurchaseValidationAsync(profileID, storeID, itemID);
                if (checkOfferResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionPreStorePurchaseResult>(checkOfferResult.Error);
                }
                else
                {
                    return new ExecuteResult<FunctionPreStorePurchaseResult>
                    {
                        Result = new FunctionPreStorePurchaseResult
                        {
                            Available = true
                        }
                    };
                }
            }
            else
            {
                var getContainerResult = await GetStoreContainerAsync();
                if (getContainerResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionPreStorePurchaseResult>(getContainerResult.Error);
                } 
                var storeContainer = getContainerResult.Result;
                var hasLimitation = storeContainer.ContainLimitationMeta(storeID, itemID);
                if (hasLimitation)
                {
                    var periodID = StoreUtils.GetStoreItemID(storeID, itemID);
                    var limitationMeta = storeContainer.GetLimitationMeta(storeID, itemID);
                    var period = limitationMeta.LimitPeriod;
                    var requestDate = ServerTimeUTC.AddMilliseconds(timeZone);
                    var periodStateResult = await TimePeriodAssistant.GetPeriodStateAsync(LimitationTableID, profileID, periodID, period, requestDate);
                    if (periodStateResult.Error != null)
                    {
                        return ErrorHandler.ThrowError<FunctionPreStorePurchaseResult>(periodStateResult.Error);
                    }
                    var periodState = periodStateResult.Result;
                    var checkInAvailable = periodState.CheckinAvailable;

                    if (checkInAvailable)
                    {
                        return new ExecuteResult<FunctionPreStorePurchaseResult>
                        {
                            Result = new FunctionPreStorePurchaseResult
                            {
                                Available = true
                            }
                        };
                    }
                    else
                    {
                        var rawData = periodState.RawData ?? string.Empty;
                        var actualLimit = 0;
                        var parseResult = int.TryParse(rawData, out actualLimit);
                        if (parseResult)
                        {
                            if (actualLimit <= 0)
                            {
                                return ErrorHandler.StoreItemNotAvailable<FunctionPreStorePurchaseResult>();
                            }
                            else
                            {
                                return new ExecuteResult<FunctionPreStorePurchaseResult>
                                {
                                    Result = new FunctionPreStorePurchaseResult
                                    {
                                        Available = true
                                    }
                                };
                            }
                        }
                        else
                        {
                            return new ExecuteResult<FunctionPreStorePurchaseResult>
                            {
                                Result = new FunctionPreStorePurchaseResult
                                {
                                    Available = true
                                }
                            };
                        }
                    }
                }
                else
                {
                    return new ExecuteResult<FunctionPreStorePurchaseResult>
                    {
                        Result = new FunctionPreStorePurchaseResult
                        {
                            Available = true
                        }
                    };
                }
            }
        }

        public static async Task<ExecuteResult<FunctionPostStorePurchaseResult>> PostStorePurchaseProcessAsync(FunctionValidateStorePurchaseRequest request)
        {
            var profileID = request.ProfileID;
            var itemID = request.ItemID;
            var checkPack = request.IsPack;
            var storeID = request.StoreID;
            var timeZone = request.TimeZoneOffset;
            Dictionary<string, uint> purchasedCurrencies = null;
            StoreLimitationInfo limitationInfo = null;
            
            var requestDate = ServerTimeUTC.AddMilliseconds(timeZone);

            // check pack
            if (checkPack)
            {
                var postResult = await ItemsModule.PostPurchaseProccessAsync(profileID, itemID);
                if (postResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionPostStorePurchaseResult>(postResult.Error);
                }
                purchasedCurrencies = postResult.Result.PurchasedCurrencies;
            }

            // check special offer
            var isSpecialOffer = IsSpecialOffer(storeID);
            if (isSpecialOffer)
            {
                var confirmResult = await SpecialOfferModule.ConfirmSpecialOfferPurchaseAsync(profileID, storeID, itemID);
                if (confirmResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionPostStorePurchaseResult>(confirmResult.Error);
                }
            }
            else
            {
                // check limitation
                var getContainerResult = await GetStoreContainerAsync();
                if (getContainerResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionGetStoresResult>(getContainerResult.Error);
                } 
                var storeContainer = getContainerResult.Result;
                var hasLimitation = storeContainer.ContainLimitationMeta(storeID, itemID);
                if (hasLimitation)
                {
                    var limitationMeta = storeContainer.GetLimitationMeta(storeID, itemID);
                    if (limitationMeta != null)
                    {
                        var periodID = StoreUtils.GetStoreItemID(storeID, itemID);
                        var rawDataResult = await TimePeriodAssistant.GetPeriodRawDataAsync(LimitationTableID, profileID, periodID);
                        if (rawDataResult.Error != null)
                        {
                            return ErrorHandler.ThrowError<FunctionPostStorePurchaseResult>(rawDataResult.Error);
                        }
                        var rawData = rawDataResult.Result ?? string.Empty;
                        var actualLimit = 0;
                        var parseResult = int.TryParse(rawData, out actualLimit);
                        if (!parseResult)
                        {
                            actualLimit = limitationMeta.MaxQuantity;
                        }
                        // decrease limit
                        actualLimit--;

                        // check in
                        var checkInResult = await TimePeriodAssistant.CheckIn(LimitationTableID, profileID, periodID, requestDate, limitationMeta.LimitPeriod, actualLimit.ToString());
                        if (checkInResult.Error != null)
                        {
                            return ErrorHandler.ThrowError<FunctionPostStorePurchaseResult>(checkInResult.Error);
                        }
                        var checkInData = checkInResult.Result;

                        limitationInfo = new StoreLimitationInfo
                        {
                            LimitPeriod = limitationMeta.LimitPeriod,
                            MaxQuantity = limitationMeta.MaxQuantity,
                            LeftQuantity = actualLimit,
                            ResetLimitDate = checkInData.DateOfNextCheckIn
                        };
                    }
                }
            }

            return new ExecuteResult<FunctionPostStorePurchaseResult>
            {
                Result = new FunctionPostStorePurchaseResult
                {
                    PurchasedCurrencies = purchasedCurrencies,
                    Limitation = limitationInfo,
                }
            };
        }

        public static async Task<ExecuteResult<FunctionRevokeLimitationResult>> RevokeItemLimitationAsync(string profileID, string storeID, string itemID)
        {
            var periodID = StoreUtils.GetStoreItemID(storeID, itemID);
            var removeResult = await TimePeriodAssistant.RemovePeriodInstanceAsync(LimitationTableID, profileID, periodID);
            if (removeResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionRevokeLimitationResult>(removeResult.Error);
            }
            return new ExecuteResult<FunctionRevokeLimitationResult>
            {
                Result = new FunctionRevokeLimitationResult
                {
                    ProfileID = profileID,
                    StoreID = storeID,
                    ItemID = itemID
                }
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SetStoreActivityAsync(string storeID, bool activity)
        {
            var adminAPI = await GetFabAdminAPIAsync();
            var storeRequest = new PlayFab.AdminModels.GetStoreItemsRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = storeID
            };
            var getStoreResult = await adminAPI.GetStoreItemsAsync(storeRequest);
            if (getStoreResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(getStoreResult.Error);
            }
            var originStore = getStoreResult.Result;

            var storeMeta = originStore.MarketingData.Metadata;
            var storeMetaRaw = storeMeta == null ? JsonPlugin.EMPTY_JSON : storeMeta.ToString();
            var storeData = JsonPlugin.FromJson<CBSStoreMeta>(storeMetaRaw);
            storeData.Enable = activity;

            originStore.MarketingData.Metadata = JsonPlugin.ToJson(storeData);

            var updateResult = await UpdateStoreAsync(originStore);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SetStoreItemPriceAsync(string storeID, string itemID, string curCode, int curValue)
        {
            var adminAPI = await GetFabAdminAPIAsync();
            var storeRequest = new PlayFab.AdminModels.GetStoreItemsRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = storeID
            };
            var getStoreResult = await adminAPI.GetStoreItemsAsync(storeRequest);
            if (getStoreResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(getStoreResult.Error);
            }
            var originStore = getStoreResult.Result;

            var items = originStore.Store;

            var itemToSet = items.FirstOrDefault(x=>x.ItemId == itemID);
            if (itemToSet == null)
            {
                return ErrorHandler.ItemInstanceNotFound<FunctionEmptyResult>();
            }

            var currencies = itemToSet.VirtualCurrencyPrices ?? new Dictionary<string, uint>();
            currencies[curCode] = (uint)curValue;
            itemToSet.VirtualCurrencyPrices = currencies;

            var updateResult = await UpdateStoreAsync(originStore);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> SetStoreItemActivityAsync(string storeID, string itemID, bool activity)
        {
            var adminAPI = await GetFabAdminAPIAsync();
            var storeRequest = new PlayFab.AdminModels.GetStoreItemsRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = storeID
            };
            var getStoreResult = await adminAPI.GetStoreItemsAsync(storeRequest);
            if (getStoreResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(getStoreResult.Error);
            }
            var originStore = getStoreResult.Result;

            var items = originStore.Store;

            var itemToSet = items.FirstOrDefault(x=>x.ItemId == itemID);
            if (itemToSet == null)
            {
                return ErrorHandler.ItemInstanceNotFound<FunctionEmptyResult>();
            }

            var slotRawData = itemToSet.CustomData == null ? JsonPlugin.EMPTY_JSON : itemToSet.CustomData.ToString();
            var metaData = new CBSStoreItemMeta();
            try
            {
                metaData = JsonPlugin.FromJsonDecompress<CBSStoreItemMeta>(slotRawData);
            }
            catch
            {
                metaData = JsonPlugin.FromJson<CBSStoreItemMeta>(slotRawData);
            }
            metaData.Enable = activity;

            itemToSet.CustomData = JsonPlugin.ToJsonCompress(metaData);

            var updateResult = await UpdateStoreAsync(originStore);
            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(updateResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> UpdateStoreAsync(PlayFab.AdminModels.GetStoreItemsResult store)
        {
            var adminAPI = await GetFabAdminAPIAsync();
            var request = new UpdateStoreItemsRequest
            {
                StoreId = store.StoreId,
                CatalogVersion = store.CatalogVersion,
                MarketingData = store.MarketingData,
                Store = store.Store
            };

            var setResult = await adminAPI.SetStoreItemsAsync(request);
            if (setResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(setResult.Error);
            }

            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        // internal
        private static bool IsSpecialOffer(string storeID)
        {
            return storeID == StoresContainer.GLOBAL_OFFER_STORE_ID || storeID == StoresContainer.PROFILE_OFFER_STORE_ID;
        }
    }
}