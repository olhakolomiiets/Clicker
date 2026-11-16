using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Samples.Purchasing.IAP5.Demo
{
    public class IAPPaywallCallbacks
    {
        readonly PaywallManager m_PaywallManager;

        public IAPPaywallCallbacks(PaywallManager paywallManager)
        {
            m_PaywallManager = paywallManager;
        }

        public void OnInitialProductsFetched(List<Product> products)
        {
            Debug.Log($"[IAP] Products fetched: {products.Count}");
            m_PaywallManager.UpdateActivePurchaseButtons();
            m_PaywallManager.FetchExistingPurchases();
        }

        public void OnInitialProductsFetchFailed(ProductFetchFailed failure)
        {
            Debug.LogWarning($"[IAP] Products fetch failed: {failure.FailureReason}");
            m_PaywallManager.UpdateActivePurchaseButtons();
        }

        public void OnExistingPurchasesFetched(Orders existingOrders)
        {
            m_PaywallManager.HandlePurchasesFetched(existingOrders);
            if (existingOrders == null)
                return;

            foreach (var order in existingOrders.PendingOrders)
            {
                m_PaywallManager.QueuePendingOrder(order);
            }

            Debug.Log("[IAP] Existing purchases fetched.");
            m_PaywallManager.UpdateActivePurchaseButtons();
        }

        public void OnExistingPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            Debug.LogWarning($"[IAP] Purchases fetch failed: {failure.Message}");
            m_PaywallManager.HandlePurchasesFetchFailed();
            m_PaywallManager.UpdateActivePurchaseButtons();
        }

        public void OnPurchasePending(PendingOrder order)
        {
            m_PaywallManager.HandlePurchasePending(order);
        }

        public void OnPurchaseConfirmed(Order order)
        {
            switch (order)
            {
                case FailedOrder failedOrder:
                    OnConfirmationFailed(failedOrder);
                    break;
                case ConfirmedOrder confirmedOrder:
                    OnPurchaseConfirmed(confirmedOrder);
                    break;
            }
        }

        void OnConfirmationFailed(FailedOrder failedOrder)
        {
            m_PaywallManager.HandlePurchaseConfirmation(failedOrder);
            Debug.LogWarning($"[IAP] Purchase confirmation failed: {failedOrder.FailureReason}");
        }

        public void OnPurchaseConfirmed(ConfirmedOrder order)
        {
            m_PaywallManager.HandlePurchaseConfirmation(order);
            Debug.Log("[IAP] Purchase confirmed.");
        }

        public void OnPurchaseFailed(FailedOrder failedOrder)
        {
            m_PaywallManager.HandlePurchaseFailed(failedOrder);
            Debug.LogWarning($"[IAP] Purchase failed: {failedOrder.FailureReason}");
        }

        public void OnOrderDeferred(DeferredOrder deferredOrder)
        {
            m_PaywallManager.HandlePurchaseDeferred(deferredOrder);
            Debug.Log("[IAP] Purchase deferred.");
        }
    }
}
