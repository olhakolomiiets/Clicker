using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.Purchasing.IAP5.Demo
{
    [RequireComponent(typeof(Button))]
    public class ProductPurchaseButtonHelper : MonoBehaviour
    {
        public string productId;
        public TextMeshProUGUI priceText;

        private Button m_Button;
        private PaywallManager m_RegisteredPaywall;

        void Awake()
        {
            m_Button = GetComponent<Button>();
        }

        void Start()
        {
            // When a scene starts directly, the paywall may awaken after this button's OnEnable.
            if (m_RegisteredPaywall == null)
            {
                RegisterWithPaywall();
                UpdateText();
            }
        }

        void OnEnable()
        {
            m_Button.onClick.AddListener(PurchaseProduct);
            RegisterWithPaywall();
            UpdateText();
        }

        void OnDisable()
        {
            if (m_Button != null)
                m_Button.onClick.RemoveListener(PurchaseProduct);

            if (m_RegisteredPaywall != null)
                m_RegisteredPaywall.UnregisterButton(this);

            m_RegisteredPaywall = null;
        }

        void RegisterWithPaywall()
        {
            m_RegisteredPaywall = PaywallManager.Instance;
            if (m_RegisteredPaywall != null)
                m_RegisteredPaywall.RegisterButton(this);
        }

        void PurchaseProduct()
        {
            PaywallManager manager = PaywallManager.Instance;
            if (manager != null && !string.IsNullOrWhiteSpace(productId))
                manager.InitiatePurchase(productId);
        }

        internal void UpdateText()
        {
            PaywallManager manager = PaywallManager.Instance;
            var product = manager != null ? manager.FindProduct(productId) : null;
            string localizedPrice = product?.metadata?.localizedPriceString;

            if (priceText != null)
                priceText.text = string.IsNullOrWhiteSpace(localizedPrice) ? "—" : localizedPrice;

            m_Button.interactable = manager != null && manager.CanInitiatePurchase(productId);
        }
    }
}
