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
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace CBS
{
    public class SpecialOfferModule : BaseAzureModule
    {
        [FunctionName(AzureFunctions.StartSpecialOfferMethod)]
        public static async Task<dynamic> StartSpecialOfferTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var itemID = request.ID;

            var startResult = await StartSpecialOffer(itemID, starter);
            if (startResult.Error != null)
            {
                return ErrorHandler.ThrowError(startResult.Error).AsFunctionResult();
            }

            return startResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.StopSpecialOfferMethod)]
        public static async Task<dynamic> StopSpecialOfferTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var itemID = request.ID;

            var stopResult = await StopSpecialOffer(itemID, starter);
            if (stopResult.Error != null)
            {
                return ErrorHandler.ThrowError(stopResult.Error).AsFunctionResult();
            }

            return stopResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GetSpecialOffersMethod)]
        public static async Task<dynamic> GetSpecialOffersTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionBaseRequest>();
            var profileID = request.ProfileID;

            var getResult = await GetAllOffersAvailableForProfileAsync(profileID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        [FunctionName(AzureFunctions.GrantSpecialOfferToProfileMethod)]
        public static async Task<dynamic> GrantSpecialOfferToProfileTrigger([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            var request = context.GetRequest<FunctionIDRequest>();
            var profileID = request.ProfileID;
            var itemID = request.ID;

            var getResult = await GrantSpecialOfferToProfileAsync(profileID, itemID);
            if (getResult.Error != null)
            {
                return ErrorHandler.ThrowError(getResult.Error).AsFunctionResult();
            }

            return getResult.Result.AsFunctionResult();
        }

        public static async Task<ExecuteResult<FunctionStartOfferResult>> StartSpecialOffer(string itemID, IDurableOrchestrationClient starter)
        {
            // get offer store
            var storeRequest = new GetStoreItemsServerRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = StoresContainer.GLOBAL_OFFER_STORE_ID
            };
            var getStoreResult = await FabServerAPI.GetStoreItemsAsync(storeRequest);
            if (getStoreResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionStartOfferResult>(getStoreResult.Error);
            }
            var originStore = getStoreResult.Result;
            var items = originStore.Store;
            // get offer item
            var item = items.FirstOrDefault(x=>x.ItemId == itemID);
            if (item == null)
            {
                ErrorHandler.StoreItemNotFound<FunctionStartOfferResult>();
            }

            // parse item;
            var dataObject = item.CustomData;
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
            if (itemMeta.Enable)
            {
                return ErrorHandler.TaskAlreadyRunning<FunctionStartOfferResult>();
            }
            // enable item
            var instanceID = Guid.NewGuid().ToString();
            itemMeta.Enable = true;
            itemMeta.InstanceID = instanceID;

            var timeLimited = itemMeta.HasDuration;
            if (timeLimited)
            {
                var eventID = StoreUtils.GetStoreItemID(StoresContainer.GLOBAL_OFFER_STORE_ID, itemID);
                var eventSeconds = itemMeta.OfferDuration;

                var durableRequest = new FunctionDurableTaskRequest
                {
                    EventID = eventID,
                    Delay = eventSeconds,
                    FunctionName = AzureFunctions.StopSpecialOfferMethod,
                    FunctionRequest = new FunctionIDRequest
                    {
                        ID = itemID
                    }
                };
                var durableResult = await DurableTaskExecuter.StartDurableTaskAsync(durableRequest, starter);
                if (durableResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionStartOfferResult>(durableResult.Error);
                }
                var offerInstanceID = durableResult.Result.DurableTaskInstanceID;
                var endDate = durableResult.Result.ExecuteDate;

                itemMeta.InstanceID = offerInstanceID;
                itemMeta.EndDate = endDate;
            }

            var store = new List<PlayFab.AdminModels.StoreItem>();
            var storeItem = new PlayFab.AdminModels.StoreItem
            {
                ItemId = item.ItemId,
                VirtualCurrencyPrices = item.VirtualCurrencyPrices,
                RealCurrencyPrices = item.RealCurrencyPrices,
                DisplayPosition = item.DisplayPosition,
                CustomData = JsonPlugin.ToJsonCompress(itemMeta)
            };
            store.Add(storeItem);

            // save changes
            var adminAPI = await GetFabAdminAPIAsync();
            var updateRequest = new PlayFab.AdminModels.UpdateStoreItemsRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = StoresContainer.GLOBAL_OFFER_STORE_ID,
                Store = store
            };
            var updateResult = await adminAPI.UpdateStoreItemsAsync(updateRequest);

            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionStartOfferResult>(updateResult.Error);
            }

            return new ExecuteResult<FunctionStartOfferResult>
            {
                Result = new FunctionStartOfferResult
                {
                    ItemID = itemID,
                    OfferInstance = instanceID,
                    EndDate = itemMeta.EndDate
                }
            };
        }

        public static async Task<ExecuteResult<FunctionStopOfferResult>> StopSpecialOffer(string itemID, IDurableOrchestrationClient starter)
        {
            // get offer store
            var storeRequest = new GetStoreItemsServerRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = StoresContainer.GLOBAL_OFFER_STORE_ID
            };
            var getStoreResult = await FabServerAPI.GetStoreItemsAsync(storeRequest);
            if (getStoreResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionStopOfferResult>(getStoreResult.Error);
            }
            var originStore = getStoreResult.Result;
            var items = originStore.Store;
            // get offer item
            var item = items.FirstOrDefault(x=>x.ItemId == itemID);
            if (item == null)
            {
                ErrorHandler.StoreItemNotFound<FunctionStopOfferResult>();
            }

            // parse item;
            var dataObject = item.CustomData;
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
            if (!itemMeta.Enable)
            {
                return ErrorHandler.TaskAlreadyStopped<FunctionStopOfferResult>();
            }
            // disable item
            var instanceID = itemMeta.InstanceID;
            itemMeta.Enable = false;
            itemMeta.InstanceID = null;

            var timeLimited = itemMeta.HasDuration;
            if (timeLimited)
            {
                var eventID = StoreUtils.GetStoreItemID(StoresContainer.GLOBAL_OFFER_STORE_ID, itemID);
                await DurableTaskExecuter.StopDurableTaskAsync(instanceID, eventID,  starter);
            }

            var store = new List<PlayFab.AdminModels.StoreItem>();
            var storeItem = new PlayFab.AdminModels.StoreItem
            {
                ItemId = item.ItemId,
                VirtualCurrencyPrices = item.VirtualCurrencyPrices,
                RealCurrencyPrices = item.RealCurrencyPrices,
                DisplayPosition = item.DisplayPosition,
                CustomData = JsonPlugin.ToJsonCompress(itemMeta)
            };
            store.Add(storeItem);

            // save changes
            var adminAPI = await GetFabAdminAPIAsync();
            var updateRequest = new PlayFab.AdminModels.UpdateStoreItemsRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = StoresContainer.GLOBAL_OFFER_STORE_ID,
                Store = store
            };
            var updateResult = await adminAPI.UpdateStoreItemsAsync(updateRequest);

            if (updateResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionStopOfferResult>(updateResult.Error);
            }

            return new ExecuteResult<FunctionStopOfferResult>
            {
                Result = new FunctionStopOfferResult
                {
                    ItemID = itemID,
                    OfferInstance = instanceID,
                }
            };
        }

        public static async Task<ExecuteResult<FunctionSpecialOffersResult>> GetSpecialOffers(string specialOfferStoreID, bool onlyActive = false)
        {
            // get offer store
            var storeRequest = new GetStoreItemsServerRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = specialOfferStoreID
            };
            var getStoreResult = await FabServerAPI.GetStoreItemsAsync(storeRequest) ?? new PlayFab.PlayFabResult<GetStoreItemsResult>();
            if (getStoreResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSpecialOffersResult>(getStoreResult.Error);
            }
            var originStore = getStoreResult.Result ?? new GetStoreItemsResult();
            // parse items
            var fabItems = originStore.Store ?? new List<StoreItem>();
            var cbsItems = new List<CBSSpecialOffer>();
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
                var itemDisplayName = itemMeta.SlotDisplayName;
                var itemDescription = itemMeta.Description;
                var discounts = itemMeta.Discounts;
                var instanceID = itemMeta.InstanceID;
                var hasTimeLimit = itemMeta.HasDuration;
                var duration = itemMeta.OfferDuration;
                var endDate = itemMeta.EndDate;

                var cbsItem = new CBSSpecialOffer
                {
                    OfferInstanceID = instanceID,
                    HasTimeLimit = hasTimeLimit,
                    OfferEndDate = endDate,
                    Duration = duration,
                    ItemID = itemID,
                    DisplayName = itemDisplayName,
                    Description = itemDescription,
                    Prices = fabItem.VirtualCurrencyPrices,
                    StoreID = specialOfferStoreID,
                    HasQuantityLimit = false,
                    Discounts = discounts,
                    CustomDataClassName = itemMeta.CustomDataClassName,
                    CustomRawData = itemMeta.CustomRawData
                };
                if (onlyActive)
                {
                    if (itemEnabled)
                        cbsItems.Add(cbsItem);
                }
                else
                {
                    cbsItems.Add(cbsItem);
                }
            }

            return new ExecuteResult<FunctionSpecialOffersResult>
            {
                Result = new FunctionSpecialOffersResult
                {
                    Offers = cbsItems
                }
            };
        }

        public static async Task<ExecuteResult<CBSSpecialOffer>> GetOfferByItemID(string specialOfferStoreID, string itemID)
        {
            var getOffersResult = await GetSpecialOffers(specialOfferStoreID);
            if (getOffersResult.Error != null)
            {
                return ErrorHandler.ThrowError<CBSSpecialOffer>(getOffersResult.Error);
            }
            var offers = getOffersResult.Result;
            var offer = offers.Offers.FirstOrDefault(x=>x.ItemID == itemID);
            if (offer == null)
            {
                return ErrorHandler.StoreItemNotFound<CBSSpecialOffer>();
            }
            return new ExecuteResult<CBSSpecialOffer>
            {
                Result = offer
            };
        }

        public static async Task<ExecuteResult<FunctionSpecialOffersResult>> GetSpecialOffersAvailableForProfileAsync(string profileID, string specialOfferStoreID)
        {
            var getContainerResult = await GetPurchasedOffersAsync(profileID);
            if (getContainerResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSpecialOffersResult>(getContainerResult.Error);
            }
            var purchasedOffers = getContainerResult.Result ?? new OfferContainer();

            var getOffersResult = await GetSpecialOffers(specialOfferStoreID, true);
            if (getOffersResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionSpecialOffersResult>(getOffersResult.Error);
            }

            var offerResult = getOffersResult.Result ?? new FunctionSpecialOffersResult();
            var offers = offerResult.Offers ?? new List<CBSSpecialOffer>();
            var notPurchasedOffers = offers.Where(x=> !purchasedOffers.OfferPurchased(x.OfferInstanceID));

            if (specialOfferStoreID == StoresContainer.PROFILE_OFFER_STORE_ID)
            {
                var getProfileOffersResult = await GetActiveProfileOfferContainerAsync(profileID);
                if (getContainerResult.Error != null)
                {
                    return ErrorHandler.ThrowError<FunctionSpecialOffersResult>(getContainerResult.Error);
                }
                var profileOffers = getProfileOffersResult.Result;
                var allOffers = profileOffers.Offers ?? new List<ProfileOfferInfo>();
                var activeOffers = allOffers.Where(x=> profileOffers.IsActive(x.OfferInstanceID, ServerTimeUTC)).ToList();
                var availableOffers = notPurchasedOffers.Where(x=> activeOffers.Any(y=>y.ItemID == x.ItemID));

                foreach (var offer in availableOffers)
                {
                    var offerInfo = profileOffers.GetOfferByItemID(offer.ItemID);
                    offer.OfferInstanceID = offerInfo.OfferInstanceID;
                    offer.OfferEndDate = offerInfo.EndDate;
                }

                return new ExecuteResult<FunctionSpecialOffersResult>
                {
                    Result = new FunctionSpecialOffersResult
                    {
                        Offers = availableOffers.ToList()
                    }
                };
            }
            else
            {
                return new ExecuteResult<FunctionSpecialOffersResult>
                {
                    Result = new FunctionSpecialOffersResult
                    {
                        Offers = notPurchasedOffers.ToList()
                    }
                };
            }
        }

        public static async Task<ExecuteResult<ProfileOffers>> GetActiveProfileOfferContainerAsync(string profileID)
        {
            var dataResult = await BaseAzureModule.GetProfileInternalDataAsObject<ProfileOffers>(profileID, ProfileDataKeys.ProfileOffers);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<ProfileOffers>(dataResult.Error);
            }
            var container = dataResult.Result ?? new ProfileOffers();
            return new ExecuteResult<ProfileOffers>
            {
                Result = container
            };
        }

        public static async Task<ExecuteResult<OfferContainer>> GetPurchasedOffersAsync(string profileID)
        {
            var dataResult = await BaseAzureModule.GetProfileInternalDataAsObject<OfferContainer>(profileID, ProfileDataKeys.PurchasedOffers);
            if (dataResult.Error != null)
            {
                return ErrorHandler.ThrowError<OfferContainer>(dataResult.Error);
            }
            var container = dataResult.Result ?? new OfferContainer();
            return new ExecuteResult<OfferContainer>
            {
                Result = container
            };
        }

        public static async Task<ExecuteResult<FunctionSpecialOffersResult>> GetAllOffersAvailableForProfileAsync(string profileID)
        {
            var profileOffersTask = GetSpecialOffersAvailableForProfileAsync(profileID, StoresContainer.PROFILE_OFFER_STORE_ID);
            var globalOffersTask = GetSpecialOffersAvailableForProfileAsync(profileID, StoresContainer.GLOBAL_OFFER_STORE_ID);
            var allTasks = new List<Task<ExecuteResult<FunctionSpecialOffersResult>>>();
            allTasks.Add(profileOffersTask);
            allTasks.Add(globalOffersTask);
            var allResult = await Task.WhenAll(allTasks);
            if (allResult[0].Result != null || allResult[1].Result != null)
            {
                var aResult = allResult[0].Result ?? new FunctionSpecialOffersResult();
                var bResult = allResult[1].Result ?? new FunctionSpecialOffersResult();
                var aOffers = aResult.Offers ?? new List<CBSSpecialOffer>();
                var bOffers = bResult.Offers ?? new List<CBSSpecialOffer>();
                var mergredOffers = aOffers.Concat(bOffers);
                return new ExecuteResult<FunctionSpecialOffersResult>
                {
                    Result = new FunctionSpecialOffersResult
                    {
                        Offers = mergredOffers.ToList()
                    }
                };
            }
            else
            {
                return new ExecuteResult<FunctionSpecialOffersResult>
                {
                    Result = new FunctionSpecialOffersResult
                    {
                        Offers = new List<CBSSpecialOffer>()
                    }
                };
            }
        }

        public static async Task<ExecuteResult<FunctionGrantSpecialOfferResult>> GrantSpecialOfferToProfileAsync(string profileID, string itemID)
        {
            var getOfferResult = await GetOfferByItemID(StoresContainer.PROFILE_OFFER_STORE_ID, itemID);
            if (getOfferResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantSpecialOfferResult>(getOfferResult.Error);
            }
            var offer = getOfferResult.Result;

            // check availability
            var getContainerResult = await GetActiveProfileOfferContainerAsync(profileID);
            if (getContainerResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantSpecialOfferResult>(getContainerResult.Error);
            }
            var container = getContainerResult.Result ?? new ProfileOffers();
            var offers = container.Offers ?? new List<ProfileOfferInfo>();
            var info = container.GetOfferByItemID(itemID);
            if (info != null && !info.IsActive(ServerTimeUTC))
            {
                container.RemoveOutDated(ServerTimeUTC);
                offers = container.Offers ?? new List<ProfileOfferInfo>();
            }
            var offerExist = offers.Any(x=>x.ItemID == itemID);
            
            if (offerExist)
            {
                return ErrorHandler.OfferAlreadyGranted<FunctionGrantSpecialOfferResult>();
            }

            // generate new offer
            var offerInstanceID = Guid.NewGuid().ToString();
            var duration = offer.Duration;
            var hasTimeLimit = offer.HasTimeLimit;
            DateTime? endDate = hasTimeLimit ? ServerTimeUTC.AddSeconds(duration) : null;
            var offerInfo = new ProfileOfferInfo
            {
                OfferInstanceID = offerInstanceID,
                ItemID = itemID,
                StoreID = StoresContainer.PROFILE_OFFER_STORE_ID,
                EndDate = endDate
            };
            container.AddOffer(offerInfo);
            container.RemoveOutDated(ServerTimeUTC);

            // save container
            var containerRaw = JsonPlugin.ToJson(container);
            var saveResult = await SaveProfileInternalDataAsync(profileID, ProfileDataKeys.ProfileOffers, containerRaw);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGrantSpecialOfferResult>(saveResult.Error);
            }
            offer.OfferInstanceID = offerInstanceID;
            offer.OfferEndDate = endDate;
            
            return new ExecuteResult<FunctionGrantSpecialOfferResult>
            {
                Result = new FunctionGrantSpecialOfferResult
                {
                    Offer = offer
                }
            };
        }

        public static async Task<ExecuteResult<string>> PreSpecialOfferPurchaseValidationAsync(string profileID, string storeID, string itemID)
        {
            var getAvailableOffersResult = await GetSpecialOffersAvailableForProfileAsync(profileID, storeID);
            if (getAvailableOffersResult.Error != null)
            {
                return ErrorHandler.ThrowError<string>(getAvailableOffersResult.Error);
            }
            var offerResult = getAvailableOffersResult.Result;
            var offers = offerResult.Offers ?? new List<CBSSpecialOffer>();
            var offer = offers.FirstOrDefault(x=>x.ItemID == itemID);
            if (offer == null)
            {
                return ErrorHandler.StoreItemNotAvailable<string>();
            }
            var offerInstanceID = offer.OfferInstanceID;

            var getProfileOffers = await GetPurchasedOffersAsync(profileID);
            if (getProfileOffers.Error != null)
            {
                return ErrorHandler.ThrowError<string>(getProfileOffers.Error);
            }

            var container = getProfileOffers.Result ?? new OfferContainer();
            var alreadyPurchased = container.OfferPurchased(offerInstanceID);
            if (alreadyPurchased)
            {
                return ErrorHandler.AlreadyPurchasedError<string>();
            }

            return new ExecuteResult<string>
            {
                Result = string.Empty
            };
        }

        public static async Task<ExecuteResult<string>> ConfirmSpecialOfferPurchaseAsync(string profileID, string storeID, string itemID)
        {
            var getAvailableOffersResult = await GetSpecialOffersAvailableForProfileAsync(profileID, storeID);
            if (getAvailableOffersResult.Error != null)
            {
                return ErrorHandler.ThrowError<string>(getAvailableOffersResult.Error);
            }
            var offerResult = getAvailableOffersResult.Result;
            var offers = offerResult.Offers ?? new List<CBSSpecialOffer>();
            var offer = offers.FirstOrDefault(x=>x.ItemID == itemID);
            if (offer == null)
            {
                return ErrorHandler.StoreItemNotAvailable<string>();
            }
            var offerInstanceID = offer.OfferInstanceID;

            var getProfileOffers = await GetPurchasedOffersAsync(profileID);
            if (getProfileOffers.Error != null)
            {
                return ErrorHandler.ThrowError<string>(getProfileOffers.Error);
            }

            var container = getProfileOffers.Result ?? new OfferContainer();
            container.AddOffer(offerInstanceID);

            var containerRaw = JsonPlugin.ToJson(container);
            var saveResult = await SaveProfileInternalDataAsync(profileID, ProfileDataKeys.PurchasedOffers, containerRaw);
            if (saveResult.Error != null)
            {
                return ErrorHandler.ThrowError<string>(saveResult.Error);
            }

            // check profile offer
            if (storeID == StoresContainer.PROFILE_OFFER_STORE_ID)
            {
                var getContainerResult = await GetActiveProfileOfferContainerAsync(profileID);
                if (getContainerResult.Error != null)
                {
                    return ErrorHandler.ThrowError<string>(getContainerResult.Error);
                }
                var infoContainer = getContainerResult.Result ?? new ProfileOffers();
                var info = infoContainer.GetOfferByItemID(itemID);
                if (info != null)
                {
                    infoContainer.RemoveOffer(info.ItemID);
                }

                var infoRaw = JsonPlugin.ToJson(infoContainer);
                var saveInfoResult = await SaveProfileInternalDataAsync(profileID, ProfileDataKeys.ProfileOffers, infoRaw);
                if (saveInfoResult.Error != null)
                {
                    return ErrorHandler.ThrowError<string>(saveInfoResult.Error);
                }
            }

            return new ExecuteResult<string>
            {
                Result = string.Empty
            };
        }
    }
}