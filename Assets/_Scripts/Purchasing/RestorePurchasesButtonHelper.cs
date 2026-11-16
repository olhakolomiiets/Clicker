using UnityEngine;
using UnityEngine.UI;

namespace Samples.Purchasing.IAP5.Demo
{
    [RequireComponent(typeof(Button))]
    public class RestorePurchasesButtonHelper : MonoBehaviour
    {
        private Button m_Button;

        private void Awake()
        {
            m_Button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            m_Button.onClick.AddListener(RestorePurchases);
        }

        private void OnDisable()
        {
            m_Button.onClick.RemoveListener(RestorePurchases);
        }

        private void RestorePurchases()
        {
            PaywallManager paywall = PaywallManager.Instance;
            if (paywall != null)
                paywall.RestorePurchases();
        }
    }
}
