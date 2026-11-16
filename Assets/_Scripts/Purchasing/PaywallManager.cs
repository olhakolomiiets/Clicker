using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using UnityEngine.Purchasing.Security;
using AppleAppStore = UnityEngine.Purchasing.AppleAppStore;

namespace Samples.Purchasing.IAP5.Demo
{
    public class PaywallManager : MonoBehaviour
    {
        public static PaywallManager Instance { get; private set; }

        private StoreController m_StoreController;
        private IAPPaywallCallbacks m_IAPPaywallCallbacks;
        private CrossPlatformValidator m_CrossPlatformValidator;
        private GameManager m_GameContext;
        private (string Store, string TransactionId)? m_VerifiedNoAds;
        private Dictionary<string, string> m_AppleNoAdsCandidates;

        private readonly List<ProductDefinition> m_ProductDefinitions = new List<ProductDefinition>();
        private readonly List<ProductPurchaseButtonHelper> activePurchaseButtons = new List<ProductPurchaseButtonHelper>();
        private readonly Dictionary<string, PendingOrder> m_PendingOrders = new Dictionary<string, PendingOrder>();
        private readonly HashSet<string> m_AppleVerificationsInFlight = new HashSet<string>();
        private readonly HashSet<string> m_ConfirmationsInFlight = new HashSet<string>();
        private readonly HashSet<(string ProductId, string TransactionId)> m_DeferredOrders =
            new HashSet<(string ProductId, string TransactionId)>();
        private Checkout m_Checkout;

        private bool m_InitializationStarted;
        private bool m_StoreConnected;
        private bool m_InitialPurchaseFetchRequested;
        private bool m_PurchaseFetchInProgress;
        private bool m_AppleRestoreInProgress;
        private bool m_AppleNoAdsVerificationInProgress;
        private bool m_ApplicationPaused;
        private int m_StoreConnectionVersion;
        private bool m_PendingOrderPassScheduled;
        private bool m_PurchaseProcessingReady = true;

        private sealed class Checkout
        {
            public readonly string ProductId;
            public readonly HashSet<string> KnownOrderIds = new HashSet<string>();
            public readonly HashSet<string> PendingCallbacks = new HashSet<string>();

            public Checkout(string productId)
            {
                ProductId = productId;
            }
        }

        public bool IsStoreConnected => m_StoreConnected;
        public bool IsPurchaseProcessingReady => m_PurchaseProcessingReady;

        protected void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            m_IAPPaywallCallbacks = new IAPPaywallCallbacks(this);
            CreateServices();
        }

        void Start()
        {
            Initialize();
        }

        public void RegisterGameContext(GameManager gameManager)
        {
            if (gameManager == null)
                return;

            m_GameContext = gameManager;
            SchedulePendingOrderProcessing();
            VerifyLatestAppleNoAds();
            TryRestoreNoAds();
            UpdateActivePurchaseButtons();
        }

        public void UnregisterGameContext(GameManager gameManager)
        {
            if (m_GameContext == gameManager)
            {
                m_GameContext = null;
                UpdateActivePurchaseButtons();
            }
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;

            if (m_StoreController != null)
            {
                m_StoreController.OnStoreConnected -= OnStoreConnected;
                m_StoreController.OnStoreDisconnected -= OnStoreDisconnected;
                m_StoreController.OnProductsFetched -= m_IAPPaywallCallbacks.OnInitialProductsFetched;
                m_StoreController.OnProductsFetchFailed -= m_IAPPaywallCallbacks.OnInitialProductsFetchFailed;
                m_StoreController.OnPurchasesFetched -= m_IAPPaywallCallbacks.OnExistingPurchasesFetched;
                m_StoreController.OnPurchasesFetchFailed -= m_IAPPaywallCallbacks.OnExistingPurchasesFetchFailed;
                m_StoreController.OnPurchasePending -= m_IAPPaywallCallbacks.OnPurchasePending;
                m_StoreController.OnPurchaseConfirmed -= m_IAPPaywallCallbacks.OnPurchaseConfirmed;
                m_StoreController.OnPurchaseFailed -= m_IAPPaywallCallbacks.OnPurchaseFailed;
                m_StoreController.OnPurchaseDeferred -= m_IAPPaywallCallbacks.OnOrderDeferred;
            }

            Instance = null;
        }

        public void Initialize()
        {
            if (Instance != this || m_InitializationStarted)
                return;

            m_InitializationStarted = true;

            InitCatalog();
            InitializeIapService();
            CreateCrossPlatformValidator();

            // Codeless IAP can connect the shared store before Start.
            if (m_StoreConnected)
            {
                FetchInitialProducts();
            }
            else
            {
                ConnectToStore();
            }
        }

        void InitCatalog()
        {
            m_ProductDefinitions.Clear();

            var catalog = ProductCatalog.LoadDefaultCatalog();
            var storeName = DefaultStoreHelper.GetDefaultStoreName();

            foreach (var item in catalog.allValidProducts)
            {
                string storeId = item.GetStoreID(storeName);
                m_ProductDefinitions.Add(new ProductDefinition(
                    item.id, string.IsNullOrEmpty(storeId) ? item.id : storeId, item.type));
            }

            Debug.Log($"[IAP] Loaded {m_ProductDefinitions.Count} products from IAP Catalog.");
        }

        void CreateServices()
        {
            m_StoreController = UnityIAPServices.StoreController();

            ConfigureServiceCallbacks();
        }

        void ConfigureServiceCallbacks()
        {
            m_StoreController.OnStoreConnected += OnStoreConnected;
            m_StoreController.OnStoreDisconnected += OnStoreDisconnected;

            ConfigureProductServiceCallbacks();
            ConfigurePurchasingServiceCallbacks();
        }

        void ConfigureProductServiceCallbacks()
        {
            m_StoreController.OnProductsFetched += m_IAPPaywallCallbacks.OnInitialProductsFetched;
            m_StoreController.OnProductsFetchFailed += m_IAPPaywallCallbacks.OnInitialProductsFetchFailed;
        }

        void ConfigurePurchasingServiceCallbacks()
        {
            m_StoreController.OnPurchasesFetched += m_IAPPaywallCallbacks.OnExistingPurchasesFetched;
            m_StoreController.OnPurchasesFetchFailed += m_IAPPaywallCallbacks.OnExistingPurchasesFetchFailed;
            m_StoreController.OnPurchasePending += m_IAPPaywallCallbacks.OnPurchasePending;
            m_StoreController.OnPurchaseConfirmed += m_IAPPaywallCallbacks.OnPurchaseConfirmed;
            m_StoreController.OnPurchaseFailed += m_IAPPaywallCallbacks.OnPurchaseFailed;
            m_StoreController.OnPurchaseDeferred += m_IAPPaywallCallbacks.OnOrderDeferred;
        }

        public void UpdateActivePurchaseButtons()
        {
            activePurchaseButtons.RemoveAll(button => button == null);
            foreach (var button in activePurchaseButtons.ToArray())
            {
                if (button != null && button.isActiveAndEnabled)
                    button.UpdateText();
            }
        }

        public void FetchExistingPurchases()
        {
            if (!m_StoreConnected || m_StoreController == null ||
                m_InitialPurchaseFetchRequested || m_PurchaseFetchInProgress || m_AppleRestoreInProgress)
                return;

            m_InitialPurchaseFetchRequested = true;
            FetchPurchases();
        }

        public void HandlePurchasesFetchFailed()
        {
            m_PurchaseFetchInProgress = false;
            m_InitialPurchaseFetchRequested = false;
        }

        public void RestorePurchases()
        {
            if (!CanProcessPendingOrders() || m_Checkout != null)
            {
                Debug.LogWarning("[IAP] Restore is not available.");
                return;
            }

            if (m_PurchaseFetchInProgress || m_AppleRestoreInProgress)
                return;

            if (GetSupportedStoreName() != AppleAppStore.Name)
            {
                FetchPurchases();
                return;
            }

            // The SDK starts its own fetch before invoking the sync callback.
            m_AppleRestoreInProgress = true;
            m_PurchaseFetchInProgress = true;
            int connectionVersion = m_StoreConnectionVersion;
            try
            {
                UpdateActivePurchaseButtons();
                m_StoreController.RestoreTransactions((success, _) => OnTransactionsRestored(success, connectionVersion));
            }
            catch (Exception)
            {
                OnTransactionsRestored(false, connectionVersion);
            }
        }

        private void OnTransactionsRestored(bool success, int connectionVersion)
        {
            if (Instance != this || connectionVersion != m_StoreConnectionVersion || !m_AppleRestoreInProgress)
                return;

            m_AppleRestoreInProgress = false;
            if (!success)
            {
                HandlePurchasesFetchFailed();
                Debug.LogWarning("[IAP] Apple restore sync failed or was canceled.");
            }
            UpdateActivePurchaseButtons();
        }

        private void FetchPurchases()
        {
            if (m_PurchaseFetchInProgress)
                return;

            m_PurchaseFetchInProgress = true;
            try
            {
                m_StoreController.FetchPurchases();
            }
            catch (Exception exception)
            {
                HandlePurchasesFetchFailed();
                Debug.LogWarning($"[IAP] Purchase fetch could not start ({exception.GetType().Name}).");
            }
        }

        internal void HandlePurchasesFetched(Orders orders)
        {
            m_PurchaseFetchInProgress = false;
            m_VerifiedNoAds = null;
            m_AppleNoAdsCandidates = null;
            if (orders != null)
            {
                RememberCheckoutOrders(orders.ConfirmedOrders);
                RememberCheckoutOrders(orders.PendingOrders);
                RememberCheckoutOrders(orders.DeferredOrders);
                foreach (DeferredOrder order in orders.DeferredOrders)
                    RememberDeferredOrder(order);
            }

            if (!HasReadyStore() || orders == null)
                return;

            string store = GetSupportedStoreName();
            if (store == GooglePlay.Name)
            {
                string transactionId = FindVerifiedNoAdsTransactionId(orders);
                if (transactionId != null)
                    m_VerifiedNoAds = (store, transactionId);
                TryRestoreNoAds();
            }
            else if (store == AppleAppStore.Name)
            {
                var pendingIds = new HashSet<string>(orders.PendingOrders.Select(order => order?.Info?.TransactionID));
                var candidates = new Dictionary<string, string>();
                foreach (ConfirmedOrder order in orders.ConfirmedOrders)
                {
                    var definition = GetNoAdsDefinition(order);
                    if (definition != null && order.Info.Apple?.StoreName == store &&
                        !pendingIds.Contains(order.Info.TransactionID))
                        candidates[order.Info.TransactionID] = definition.storeSpecificId;
                }

                if (candidates.Count > 0)
                    m_AppleNoAdsCandidates = candidates;
                VerifyLatestAppleNoAds();
            }
        }

        private string FindVerifiedNoAdsTransactionId(Orders orders)
        {
            foreach (ConfirmedOrder order in orders.ConfirmedOrders)
            {
                var definition = GetNoAdsDefinition(order);
                if (definition != null && ValidateGooglePlayPurchase(order.Info, definition.storeSpecificId))
                    return order.Info.TransactionID;
            }

            return null;
        }

        private static ProductDefinition GetNoAdsDefinition(ConfirmedOrder order)
        {
            var items = order?.CartOrdered?.Items();
            if (order?.Info == null || string.IsNullOrWhiteSpace(order.Info.TransactionID) ||
                items == null || items.Count != 1 || items[0]?.Quantity != 1)
                return null;

            var definition = items[0].Product?.definition;
            return definition?.id == SaveSystem.NoAdsProductId && definition.type == ProductType.NonConsumable &&
                   !string.IsNullOrWhiteSpace(definition.storeSpecificId) ? definition : null;
        }

        private async void VerifyLatestAppleNoAds()
        {
            var candidates = m_AppleNoAdsCandidates;
            if (candidates == null || m_AppleNoAdsVerificationInProgress || !HasReadyStore() ||
                GetSupportedStoreName() != AppleAppStore.Name || SaveSystem.IsIapRecoveryRequired)
                return;

            m_AppleNoAdsVerificationInProgress = true;
            AppleStoreKit2Validator.Result result = default;
            try
            {
                result = await AppleStoreKit2Validator.VerifyNoAdsEntitlementAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[IAP] Apple No Ads verification failed ({exception.GetType().Name}).");
            }
            finally
            {
                m_AppleNoAdsVerificationInProgress = false;
            }

            if (!ReferenceEquals(candidates, m_AppleNoAdsCandidates))
            {
                VerifyLatestAppleNoAds();
                return;
            }

            m_AppleNoAdsCandidates = null;
            if (!HasReadyStore() || GetSupportedStoreName() != AppleAppStore.Name ||
                result.Status != AppleStoreKit2Validator.VerificationStatus.Verified ||
                string.IsNullOrWhiteSpace(result.TransactionId) ||
                !candidates.TryGetValue(result.TransactionId, out string storeProductId) ||
                storeProductId != result.StoreProductId)
                return;

            m_VerifiedNoAds = (AppleAppStore.Name, result.TransactionId);
            TryRestoreNoAds();
        }

        private void TryRestoreNoAds()
        {
            if (!m_VerifiedNoAds.HasValue || !CanProcessPendingOrders())
                return;

            var entitlement = m_VerifiedNoAds.Value;
            if (entitlement.Store != GetSupportedStoreName())
                return;

            try
            {
                if (m_GameContext.RestoreNoAds(entitlement.Store, entitlement.TransactionId))
                    m_VerifiedNoAds = null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[IAP] No Ads could not be restored ({exception.GetType().Name}).");
            }
        }

        public static bool IsReceiptAvailable(Orders existingOrders)
        {
            return existingOrders != null &&
                   (existingOrders.ConfirmedOrders.Any(order => !string.IsNullOrEmpty(order.Info.Receipt)) ||
                    existingOrders.PendingOrders.Any(order => !string.IsNullOrEmpty(order.Info.Receipt)));
        }

        void InitializeIapService()
        {
            IAPService.Initialize(OnServiceInitialized, (message) =>
            {
                //m_IAPLogger.LogConsole($"Initialization failed, IAP service dependency error: {message}");
                Debug.Log($"Initialization failed, IAP service dependency error: {message}");
            });
        }

        void CreateCrossPlatformValidator()
        {
#if !UNITY_EDITOR
            try
            {
                if (IsGooglePlay())
                {
                    m_CrossPlatformValidator = new CrossPlatformValidator(GooglePlayTangle.Data(), Application.identifier);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[IAP] Google Play validator could not be initialized ({exception.GetType().Name}).");
            }
#endif
        }

        void OnServiceInitialized()
        {
            InitializeUI();
        }

        protected void InitializeUI()
        {
            //m_IAPLogger.inAppConsole.text = string.Empty;
        }

        async void ConnectToStore()
        {
            try
            {
                await m_StoreController.Connect();
            }
            catch (Exception exception)
            {
                if (Instance != this)
                    return;

                m_StoreConnected = false;
                Debug.LogError($"[IAP] Store connection failed: {exception.Message}");
                UpdateActivePurchaseButtons();
            }
        }

        private void OnStoreConnected()
        {
            if (m_StoreConnected)
                return;

            m_StoreConnected = true;
            Debug.Log("[IAP] Store connected.");

            if (m_InitializationStarted)
            {
                FetchInitialProducts();
            }

            SchedulePendingOrderProcessing();
            UpdateActivePurchaseButtons();
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            m_StoreConnected = false;
            m_StoreConnectionVersion++;
            m_VerifiedNoAds = null;
            m_AppleNoAdsCandidates = null;
            m_AppleRestoreInProgress = false;
            HandlePurchasesFetchFailed();
            Debug.LogWarning($"[IAP] Store disconnected: {failure}");
            UpdateActivePurchaseButtons();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Unity IAP 5.2.1's focus query can replace the shared fetch callbacks.
            if (!hasFocus && m_PurchaseFetchInProgress && IsGooglePlay())
                HandlePurchasesFetchFailed();
        }

        private void OnApplicationPause(bool paused)
        {
            if (m_ApplicationPaused == paused)
                return;

            m_ApplicationPaused = paused;
            if (!paused)
            {
                SchedulePendingOrderProcessing();
                TryRestoreNoAds();
            }
        }

        void FetchInitialProducts()
        {
            if (!m_StoreConnected || m_StoreController == null)
                return;

            if (m_ProductDefinitions.Count == 0)
            {
                Debug.LogWarning("[IAP] Product catalog is empty.");
                return;
            }

            m_StoreController.FetchProducts(m_ProductDefinitions);
        }

        public bool CanInitiatePurchase(string productId)
        {
            if (m_Checkout != null || m_AppleRestoreInProgress || !CanProcessPendingOrders() ||
                string.IsNullOrWhiteSpace(productId) || IsLegacyPurchase(productId) ||
                !PurchaseManager.IsProductAllowedInShop(productId, m_GameContext.gameObject.scene.name) ||
                m_DeferredOrders.Any(order => order.ProductId == productId) ||
                m_PendingOrders.Values.Any(order => order.CartOrdered?.Items().Any(
                    item => item?.Product?.definition?.id == productId) == true))
                return false;

            Product product = FindProduct(productId);
            return product != null && product.definition?.id == productId && product.availableToPurchase &&
                   (product.definition.type == ProductType.Consumable || product.definition.type == ProductType.NonConsumable) &&
                   !string.IsNullOrWhiteSpace(product.metadata?.localizedPriceString);
        }

        public void InitiatePurchase(string productId)
        {
            if (!CanInitiatePurchase(productId))
            {
                Debug.LogWarning($"[IAP] Cannot initiate purchase for product: {productId}");
                return;
            }

            var checkout = new Checkout(productId);
            m_Checkout = checkout;
            try
            {
                RememberCheckoutOrders(m_StoreController.GetPurchases());
                UpdateActivePurchaseButtons();
                m_StoreController.PurchaseProduct(productId);
            }
            catch (Exception exception)
            {
                EndCheckout(checkout);
                Debug.LogWarning($"[IAP] Purchase could not start ({exception.GetType().Name}).");
            }
        }

        public Product FindProduct(string productId)
        {
            if (m_StoreController == null || string.IsNullOrWhiteSpace(productId))
                return null;

            return m_StoreController.GetProductById(productId);
        }

        public ReadOnlyObservableCollection<Product> GetFetchedProducts()
        {
            return m_StoreController?.GetProducts();
        }

        public void RegisterButton(ProductPurchaseButtonHelper button)
        {
            if (button != null && !activePurchaseButtons.Contains(button))
                activePurchaseButtons.Add(button);
        }

        public void UnregisterButton(ProductPurchaseButtonHelper button)
        {
            activePurchaseButtons.Remove(button);
        }

        public void QueuePendingOrder(PendingOrder order)
        {
            if (order == null || order.Info == null)
                return;

            string transactionId = order.Info.TransactionID;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                Debug.LogError("[IAP] Pending order has no TransactionID.");
                return;
            }

            bool isNew = !m_PendingOrders.ContainsKey(transactionId);
            m_PendingOrders[transactionId] = order;
            string productId = GetOrderProductId(order);
            if (productId != null)
            {
                m_DeferredOrders.Remove((productId, transactionId));
                if (isNew)
                    m_DeferredOrders.Remove((productId, null));
            }

            if (!m_ConfirmationsInFlight.Contains(transactionId))
                SchedulePendingOrderProcessing();
        }

        internal void HandlePurchasePending(PendingOrder order)
        {
            var checkout = m_Checkout;
            string transactionId = order?.Info?.TransactionID;
            bool matches = checkout != null && checkout.ProductId == GetOrderProductId(order) &&
                           !string.IsNullOrWhiteSpace(transactionId) && order.CartOrdered.Items()[0].Quantity == 1 &&
                           !m_PendingOrders.ContainsKey(transactionId) && !checkout.KnownOrderIds.Contains(transactionId);

            QueuePendingOrder(order);
            if (matches && checkout.PendingCallbacks.Add(transactionId) && checkout.PendingCallbacks.Count == 1)
                StartCoroutine(ResolveCheckoutNextFrame(checkout));
            UpdateActivePurchaseButtons();
        }

        private IEnumerator ResolveCheckoutNextFrame(Checkout checkout)
        {
            // IAP 5.2.1 can emit pending callbacks before the related fetch callback.
            yield return null;
            if (!ReferenceEquals(m_Checkout, checkout))
                yield break;

            checkout.PendingCallbacks.ExceptWith(checkout.KnownOrderIds);
            if (checkout.PendingCallbacks.Count == 1)
                EndCheckout(checkout);
            else
            {
                checkout.KnownOrderIds.UnionWith(checkout.PendingCallbacks);
                checkout.PendingCallbacks.Clear();
            }
        }

        internal void HandlePurchaseFailed(FailedOrder order)
        {
            var checkout = m_Checkout;
            if (checkout == null || order == null || GetOrderProductId(order) != checkout.ProductId ||
                !string.IsNullOrWhiteSpace(order.Info?.TransactionID) ||
                order.FailureReason == PurchaseFailureReason.ExistingPurchasePending)
                return;

            // StoreKit can report a redelivered, already confirmed order as a duplicate with no order ID.
            if (order.FailureReason == PurchaseFailureReason.DuplicateTransaction && GetSupportedStoreName() == AppleAppStore.Name)
                return;

            EndCheckout(checkout);
        }

        internal void HandlePurchaseDeferred(DeferredOrder order)
        {
            string productId = GetOrderProductId(order);
            if (!RememberDeferredOrder(order))
                return;

            var checkout = m_Checkout;
            if (checkout != null && checkout.ProductId == productId &&
                !checkout.KnownOrderIds.Contains(order.Info?.TransactionID))
                EndCheckout(checkout);
            else
                UpdateActivePurchaseButtons();
        }

        private bool RememberDeferredOrder(DeferredOrder order)
        {
            string productId = GetOrderProductId(order);
            string transactionId = order?.Info?.TransactionID;
            return !string.IsNullOrWhiteSpace(productId) &&
                   m_DeferredOrders.Add((productId, string.IsNullOrWhiteSpace(transactionId) ? null : transactionId));
        }

        private void RememberCheckoutOrders(IEnumerable<Order> orders)
        {
            var checkout = m_Checkout;
            if (checkout == null || orders == null)
                return;

            foreach (Order order in orders)
            {
                string transactionId = order?.Info?.TransactionID;
                if (!string.IsNullOrWhiteSpace(transactionId) && GetOrderProductId(order) == checkout.ProductId)
                    checkout.KnownOrderIds.Add(transactionId);
            }
        }

        private static string GetOrderProductId(Order order)
        {
            var items = order?.CartOrdered?.Items();
            return items?.Count == 1 ? items[0]?.Product?.definition?.id : null;
        }

        private void EndCheckout(Checkout checkout)
        {
            if (!ReferenceEquals(m_Checkout, checkout))
                return;

            m_Checkout = null;
            UpdateActivePurchaseButtons();
        }

        private bool HasReadyStore()
        {
            return Instance == this && m_PurchaseProcessingReady &&
                   m_StoreConnected && m_StoreController != null;
        }

        private bool CanProcessPendingOrders()
        {
            return HasReadyStore() && !m_ApplicationPaused &&
                   m_GameContext != null && m_GameContext.isActiveAndEnabled &&
                   !SaveSystem.IsIapRecoveryRequired && GetSupportedStoreName() != null;
        }

        private void SchedulePendingOrderProcessing()
        {
            if (m_PendingOrderPassScheduled || m_PendingOrders.Count == 0 || !CanProcessPendingOrders())
                return;

            m_PendingOrderPassScheduled = true;
            StartCoroutine(ProcessPendingOrdersNextFrame());
        }

        private IEnumerator ProcessPendingOrdersNextFrame()
        {
            // Events raised during the pass can schedule another frame without re-entering this pass.
            yield return null;
            m_PendingOrderPassScheduled = false;

            if (!CanProcessPendingOrders())
                yield break;

            GameManager context = m_GameContext;
            foreach (string transactionId in m_PendingOrders.Keys.ToArray())
            {
                if (!CanProcessPendingOrders() || m_GameContext != context)
                    yield break;

                if (!m_ConfirmationsInFlight.Contains(transactionId) &&
                    m_PendingOrders.TryGetValue(transactionId, out PendingOrder order))
                {
                    ProcessPendingOrder(order, transactionId, context);
                }
            }
        }

        private void ProcessPendingOrder(PendingOrder order, string transactionId, GameManager context)
        {
            var items = order?.CartOrdered?.Items();
            if (order?.Info == null || order.Info.TransactionID != transactionId || items == null || items.Count != 1 ||
                items[0]?.Quantity != 1 || items[0].Product?.definition == null)
            {
                return;
            }

            var definition = items[0].Product.definition;
            if (string.IsNullOrWhiteSpace(definition.id) || string.IsNullOrWhiteSpace(definition.storeSpecificId) ||
                (definition.type != ProductType.Consumable && definition.type != ProductType.NonConsumable))
            {
                return;
            }

            string store = GetSupportedStoreName();
            if (store == GooglePlay.Name)
            {
                if (ValidateGooglePlayPurchase(order.Info, definition.storeSpecificId))
                    GrantAndConfirm(order, store, transactionId, definition.id, context);
            }
            else if (store == AppleAppStore.Name && m_AppleVerificationsInFlight.Add(transactionId))
            {
                VerifyApplePendingOrder(order, transactionId, definition, m_StoreConnectionVersion);
            }
        }

        private async void VerifyApplePendingOrder(PendingOrder order, string transactionId,
            ProductDefinition definition, int connectionVersion)
        {
            AppleStoreKit2Validator.Result result = default;
            try
            {
                result = await AppleStoreKit2Validator.VerifyPendingOrderAsync(order);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[IAP] Apple purchase verification failed ({exception.GetType().Name}).");
            }
            finally
            {
                m_AppleVerificationsInFlight.Remove(transactionId);
            }

            if (!CanProcessPendingOrders() || GetSupportedStoreName() != AppleAppStore.Name ||
                !m_PendingOrders.TryGetValue(transactionId, out PendingOrder currentOrder) ||
                m_ConfirmationsInFlight.Contains(transactionId))
                return;

            if (connectionVersion != m_StoreConnectionVersion || !ReferenceEquals(currentOrder, order))
            {
                // A newer delivery or reconnect may have skipped the ID while verification was in flight.
                SchedulePendingOrderProcessing();
                return;
            }

            if (result.Status == AppleStoreKit2Validator.VerificationStatus.Verified &&
                result.TransactionId == transactionId && result.TransactionId == order.Info.TransactionID &&
                result.StoreProductId == definition.storeSpecificId)
                GrantAndConfirm(order, AppleAppStore.Name, transactionId, definition.id, m_GameContext);
        }

        private void GrantAndConfirm(PendingOrder order, string store, string transactionId, string productId, GameManager context)
        {
            if (!CanProcessPendingOrders() || GetSupportedStoreName() != store || m_GameContext != context ||
                m_ConfirmationsInFlight.Contains(transactionId))
                return;

            GameManager.IapGrantResult result;
            try
            {
                result = context.GrantIapPurchase(store, transactionId, productId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[IAP] Pending purchase could not be granted ({exception.GetType().Name}).");
                return;
            }

            if (result != GameManager.IapGrantResult.Granted && result != GameManager.IapGrantResult.AlreadyProcessed)
                return;

            if (!CanProcessPendingOrders() || GetSupportedStoreName() != store || m_GameContext != context ||
                !m_PendingOrders.TryGetValue(transactionId, out PendingOrder currentOrder) ||
                !ReferenceEquals(currentOrder, order) || !m_ConfirmationsInFlight.Add(transactionId))
            {
                return;
            }

            try
            {
                m_StoreController.ConfirmPurchase(order);
            }
            catch (Exception exception)
            {
                m_ConfirmationsInFlight.Remove(transactionId);
                Debug.LogWarning($"[IAP] Could not send purchase confirmation ({exception.GetType().Name}).");
            }
        }

        internal void HandlePurchaseConfirmation(Order order)
        {
            if (m_ConfirmationsInFlight.Count == 0)
                return;

            string transactionId = order?.Info?.TransactionID;
            if (string.IsNullOrWhiteSpace(transactionId) || !m_ConfirmationsInFlight.Contains(transactionId))
            {
                Debug.LogWarning("[IAP] Confirmation result could not be matched to a pending request.");
                return;
            }

            // The SDK rejected a duplicate request while the original is still in flight.
            if (order is FailedOrder failure && failure.FailureReason == PurchaseFailureReason.ExistingPurchasePending)
                return;

            m_ConfirmationsInFlight.Remove(transactionId);
            if (order is ConfirmedOrder)
            {
                m_PendingOrders.Remove(transactionId);
                UpdateActivePurchaseButtons();
            }
        }

        private static bool IsLegacyPurchase(string productId)
        {
            return productId is "com.planetclicker.diamondspack50"
                or "com.planetclicker.diamondspack100"
                or "com.planetclicker.diamondspack300"
                or "com.planetclicker.starterpack"
                or "com.planetclicker.specialoffer";
        }

        private bool ValidateGooglePlayPurchase(IOrderInfo orderInfo, string storeSpecificId)
        {
            if (m_CrossPlatformValidator == null)
                return false;

            try
            {
                string receipt = orderInfo.Receipt;
                if (string.IsNullOrWhiteSpace(receipt))
                    return false;

                var wrapper = MiniJson.JsonDecode(receipt) as Dictionary<string, object>;
                if (wrapper == null || !wrapper.TryGetValue("Store", out object store) ||
                    store is not string storeName || storeName != GooglePlay.Name ||
                    !wrapper.TryGetValue("Payload", out object payload) || payload is not string googlePayload)
                {
                    return false;
                }

                var receipts = m_CrossPlatformValidator.Validate(receipt);
                if (receipts == null || receipts.Length != 1 || receipts[0] is not GooglePlayReceipt purchase ||
                    purchase.productID != storeSpecificId || purchase.purchaseToken != orderInfo.TransactionID ||
                    purchase.packageName != Application.identifier || purchase.purchaseState != GooglePurchaseState.Purchased)
                {
                    return false;
                }

                // Unity IAP verifies the signature over Payload.json and does not expose quantity on GooglePlayReceipt.
                var details = MiniJson.JsonDecode(googlePayload) as Dictionary<string, object>;
                if (details == null || !details.TryGetValue("json", out object originalJson) || originalJson is not string signedJson)
                {
                    return false;
                }

                var signedPurchase = JObject.Parse(signedJson);
                JToken state = signedPurchase["purchaseState"];
                JToken quantity = signedPurchase["quantity"];
                return state?.Type == JTokenType.Integer && (long)state == (long)GooglePurchaseState.Purchased &&
                       (quantity == null || (quantity.Type == JTokenType.Integer && (long)quantity == 1));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[IAP] Google Play receipt validation failed ({exception.GetType().Name}).");
                return false;
            }
        }

        private string GetSupportedStoreName()
        {
#if !UNITY_EDITOR
            if (IsGooglePlay())
                return GooglePlay.Name;
            if (Application.platform == RuntimePlatform.IPhonePlayer &&
                DefaultStoreHelper.GetDefaultStoreName() == AppleAppStore.Name)
                return AppleAppStore.Name;
#endif
            return null;
        }

        bool IsGooglePlay()
        {
            return Application.platform == RuntimePlatform.Android && DefaultStoreHelper.GetDefaultStoreName() == GooglePlay.Name;
        }
    }
}
