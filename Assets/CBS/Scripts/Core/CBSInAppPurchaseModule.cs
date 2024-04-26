using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if CBS_IAP
using UnityEngine.Purchasing;
#endif

namespace CBS
{
    public class CBSInAppPurchaseModule : CBSModule, ICBSInAppPurchase
#if CBS_IAP
        , IStoreListener
#endif
    {
        public event Action<IAPInitializeResult> OnInitialize;

#if CBS_IAP
        private IStoreController StoreController { get; set; }

        public bool IsInitialized => StoreController != null;
#else
        public bool IsInitialized => false;
#endif
        private IAPConfig IAPConfig { get; set; }

        public bool IsEnabled => IAPConfig.EnableIAP;

        private Dictionary<string, CBSInAppPurchaseQueue> ResultList { get; set; }

        protected override void Init()
        {
            IAPConfig = CBSScriptable.Get<IAPConfig>();
        }

        public void SetupStore(List<string> itemsIDs, List<string> currenciesPacksIDs, List<string> calendarsIDs, List<string> ticketsIDs)
        {
            if (!IsEnabled)
                return;
#if CBS_IAP
            ResultList = new Dictionary<string, CBSInAppPurchaseQueue>();
            var externalIDs = IAPConfig.ExternalIDs;
            var allIDs = new List<string>(itemsIDs);
            allIDs.AddRange(currenciesPacksIDs);
            allIDs.AddRange(calendarsIDs);
            allIDs.AddRange(ticketsIDs);
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var itemID in allIDs)
            {
                builder.AddProduct(itemID, ProductType.Consumable);
            }
            foreach (var externalProduct in externalIDs)
            {
                builder.AddProduct(externalProduct.ProductID, externalProduct.Type);
            }
            UnityPurchasing.Initialize(this, builder);
#endif
        }

        public void PurchaseItem(string itemID, string catalogID, Action<IAPPurchaseItemResult> result)
        {
            PurchaseItem(itemID, catalogID, null, result);
        }

        public void PurchaseItem(string itemID, string catalogID, string storeID, Action<IAPPurchaseItemResult> result)
        {
#if CBS_IAP
            if (!IsEnabled)
            {
                result?.Invoke(new IAPPurchaseItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.IAPNotEnabledError()
                });
                return;
            }
            if (!IsInitialized)
            {
                result?.Invoke(new IAPPurchaseItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.IAPNotInitializedError()
                });
                return;
            }
            if (ResultList.ContainsKey(itemID))
                return;
            var purchaseQueue = new CBSInAppPurchaseQueue
            {
                ItemID = itemID,
                CatalogID = catalogID,
                Result = result,
                StoreID = storeID
            };

            RegisterPurchaseAction(itemID, purchaseQueue);

            StoreController.InitiatePurchase(itemID);
#else
            result?.Invoke(new IAPPurchaseItemResult
            {
                IsSuccess = false,
                Error = CBSError.IAPNotEnabledError()
            });
#endif
        }

        private void RegisterPurchaseAction(string id, CBSInAppPurchaseQueue queue)
        {
            if (!ResultList.ContainsKey(id))
            {
                ResultList[id] = queue;
            }
        }

        private void UnRegisterPurchaseAction(string id)
        {
            if (ResultList.ContainsKey(id))
            {
                ResultList.Remove(id);
            }
        }

#if CBS_IAP
        private void ValidatePurchase(Product product)
        {
            var productID = product.definition.id;
            var profileID = Get<CBSProfileModule>().ProfileID;
            var purchaseQueue = ResultList[productID];
            purchaseQueue.TransactionID = product.transactionID;

#if UNITY_EDITOR
            // unity sandbox purchase
            var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.ValidateIAPPurchaseMethod,
                FunctionParameter = new FunctionFabItemRequest
                {
                    ProfileID = profileID,
                    CatalogID = purchaseQueue.CatalogID,
                    ItemID = productID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onValidate => 
            {
                var cbsError = onValidate.GetCBSError();
                if (cbsError != null)
                {
                    var queue = ResultList[productID];
                    var result = queue.Result;
                    result?.Invoke(new IAPPurchaseItemResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                    UnRegisterPurchaseAction(productID);
                }
                else
                {
                    var functionResult = onValidate.GetResult<FunctionValidatePurchaseResult>();;
                    var itemsInstances = functionResult.ItemsInstances;
                    PostPurchaseProcess(productID, itemsInstances);
                }
            }, onFailed =>
            {
                var queue = ResultList[productID];
                var result = queue.Result;
                result?.Invoke(new IAPPurchaseItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
                UnRegisterPurchaseAction(productID);
            });
#elif UNITY_ANDROID
            // google validation
            var googleReceipt = GooglePurchase.FromJson(product.receipt);
            var valitateRequest = new ValidateGooglePlayPurchaseRequest()
            {
                CatalogVersion = purchaseQueue.CatalogID,
                CurrencyCode = product.metadata.isoCurrencyCode,
                PurchasePrice = (uint)(product.metadata.localizedPrice * 100),
                ReceiptJson = googleReceipt.PayloadData.json,
                Signature = googleReceipt.PayloadData.signature
            };
            PlayFabClientAPI.ValidateGooglePlayPurchase(valitateRequest,
            result =>
            {
                var purchaseResults = result.Fulfillments;
                var itemsInstances = new List<ItemInstance>();
                foreach (var pRersult in purchaseResults)
                {
                    var instances = pRersult.FulfilledItems ?? new List<ItemInstance>();
                    itemsInstances = itemsInstances.Concat(instances).ToList();
                }
                PostPurchaseProcess(productID, itemsInstances);
            }, error =>
            {
                var queue = ResultList[productID];
                var result = queue.Result;
                result?.Invoke(new IAPPurchaseItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(error)
                });
                UnRegisterPurchaseAction(productID);
            });
#elif UNITY_IOS
            // ios validation
            var wrapper = (Dictionary<string, object>)MiniJson.JsonDecode(product.receipt);

            var store = (string)wrapper["Store"];
            var payload = (string)wrapper["Payload"]; // For Apple this will be the base64 encoded ASN.1 receipt

            var iosReceipt = ApplePurchase.FromJson(product.receipt);
            var iosValitateRequest = new ValidateIOSReceiptRequest()
            {
                CurrencyCode = product.metadata.isoCurrencyCode,
                PurchasePrice = (int)product.metadata.localizedPrice * 100,
                ReceiptData = payload
            };
            PlayFabClientAPI.ValidateIOSReceipt(iosValitateRequest,
            result =>
            {
                var purchaseResults = result.Fulfillments;
                var itemsInstances = new List<ItemInstance>();
                foreach (var pRersult in purchaseResults)
                {
                    var instances = pRersult.FulfilledItems ?? new List<ItemInstance>();
                    itemsInstances = itemsInstances.Concat(instances).ToList();
                }
                PostPurchaseProcess(productID, itemsInstances);
            }, error =>
            {
                var queue = ResultList[productID];
                var result = queue.Result;
                result?.Invoke(new IAPPurchaseItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(error)
                });
                UnRegisterPurchaseAction(productID);
            });
#endif
        }

        private void PostPurchaseProcess(string itemID, List<ItemInstance> itemInstances)
        {
            var profileID = Get<CBSProfileModule>().ProfileID;
            var purchaseQueue = ResultList[itemID];
            var isPack = purchaseQueue.IsPack();

            if (isPack)
            {
                itemInstances = itemInstances.Where(x => x.ItemId != itemID).ToList();
            }

            var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PostIAPProcessMethod,
                FunctionParameter = new FunctionIAPPostProcessRequest
                {
                    ProfileID = profileID,
                    CatalogID = purchaseQueue.CatalogID,
                    ItemID = itemID,
                    StoreID = purchaseQueue.StoreID,
                    IsPack = isPack,
                    TimeZoneOffset = DateUtils.GetZoneOffset()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onProcess =>
            {
                var cbsError = onProcess.GetCBSError();
                if (cbsError != null)
                {
                    var queue = ResultList[itemID];
                    var result = queue.Result;
                    result?.Invoke(new IAPPurchaseItemResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                    UnRegisterPurchaseAction(itemID);
                }
                else
                {
                    var functionResult = onProcess.GetResult<FunctionIAPPostPurchaseResult>();
                    UnityEngine.Debug.Log(JsonPlugin.ToJson(functionResult));
                    var grantedCurrencies = functionResult.GrantedCurrencies;
                    var grantedItems = itemInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                    var limitation = functionResult.LimitationInfo;

                    var queue = ResultList[itemID];
                    var result = queue.Result;
                    result?.Invoke(new IAPPurchaseItemResult
                    {
                        IsSuccess = true,
                        ProfileID = profileID,
                        GrantedCurrencies = grantedCurrencies,
                        GrantedItems = grantedItems,
                        TransactionID = purchaseQueue.TransactionID,
                        LimitationInfo = limitation
                    });

                    UnRegisterPurchaseAction(itemID);
                }
            }, onFailed =>
            {
                var queue = ResultList[itemID];
                var result = queue.Result;
                result?.Invoke(new IAPPurchaseItemResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
                UnRegisterPurchaseAction(itemID);
            });
        }

        // IAP events

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            var product = purchaseEvent.purchasedProduct;
            var transactionID = product.transactionID;
            ValidatePurchase(product);
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            var productID = product.definition.id;
            var queue = ResultList[productID];
            var result = queue.Result;
            result?.Invoke(new IAPPurchaseItemResult
            {
                IsSuccess = false,
                Error = CBSError.IAPPurchaseError(failureReason.ToString())
            });
            UnRegisterPurchaseAction(productID);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            StoreController = controller;
            var allProducts = StoreController.products.all;
            var allProductsIDs = StoreController.products.all.Select(x => x.definition.id).ToArray();
            OnInitialize?.Invoke(new IAPInitializeResult
            {
                IsSuccess = true,
                ProdutsIDs = allProductsIDs,
                Products = allProducts
            });
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            OnInitialize?.Invoke(new IAPInitializeResult
            {
                IsSuccess = false,
                Error = CBSError.IAPInitializeFailed()
            });
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            OnInitialize?.Invoke(new IAPInitializeResult
            {
                IsSuccess = false,
                Error = CBSError.IAPInitializeFailed()
            });
        }
#endif
        public class JsonData
        {
            // JSON Fields, ! Case-sensitive

            public string orderId;
            public string packageName;
            public string productId;
            public long purchaseTime;
            public int purchaseState;
            public string purchaseToken;
        }

        public class PayloadData
        {
            public JsonData JsonData;

            // JSON Fields, ! Case-sensitive
            public string signature;
            public string json;

            public static PayloadData FromJson(string json)
            {
                var payload = JsonUtility.FromJson<PayloadData>(json);
                payload.JsonData = JsonUtility.FromJson<JsonData>(payload.json);
                return payload;
            }
        }

        public class GooglePurchase
        {
            public PayloadData PayloadData;

            // JSON Fields, ! Case-sensitive
            public string Store;
            public string TransactionID;
            public string Payload;

            public static GooglePurchase FromJson(string json)
            {
                var purchase = JsonUtility.FromJson<GooglePurchase>(json);
                purchase.PayloadData = PayloadData.FromJson(purchase.Payload);
                return purchase;
            }
        }

        public class ApplePurchase
        {
            // JSON Fields, ! Case-sensitive
            public string Store;
            public string TransactionID;
            public string Payload;


            public static ApplePurchase FromJson(string json)
            {
                var purchase = JsonUtility.FromJson<ApplePurchase>(json);
                return purchase;
            }
        }
    }
}
