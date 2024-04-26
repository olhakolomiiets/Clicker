using CBS.Models;
using CBS.Scriptable;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class CurrencySlot : MonoBehaviour
    {
        [SerializeField]
        private Text ValueTitle;
        [SerializeField]
        private Image IconImage;

        private CBSCurrency Currency { get; set; }
        private CurrencyPrefabs Prefabs { get; set; }

        private void Awake()
        {
            Prefabs = CBSScriptable.Get<CurrencyPrefabs>();
        }

        public void Display(CBSCurrency currency)
        {
            Currency = currency;
            // draw ui
            ValueTitle.text = Currency.ToString();
            IconImage.sprite = Currency.GetSprite();
        }

        public void UpdateCurrency(CBSCurrency currency)
        {
            if (currency.Code == Currency.Code)
            {
                Currency = currency;
                ValueTitle.text = Currency.ToString();
            }
        }
    }
}
