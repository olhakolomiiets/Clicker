using CBS.Models;
using CBS.Scriptable;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class CurrencyItem : MonoBehaviour
    {
        [SerializeField]
        private Text ValueTitle;
        [SerializeField]
        private Image IconImage;
        [SerializeField]
        private CurrencyRechargeTimer Timer;

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
            CheckTimer();
        }

        public void UpdateCurrency(CBSCurrency currency)
        {
            if (currency.Code == Currency.Code)
            {
                Currency = currency;
                ValueTitle.text = Currency.ToString();
                CheckTimer();
            }
        }

        private void CheckTimer()
        {
            if (!Currency.Rechargeable)
            {
                Timer.gameObject.SetActive(false);
                return;
            }
            else if (!Currency.IsMaxRecharge())
            {
                Timer.gameObject.SetActive(true);
                Timer.StartTimer(Currency);
            }
            else
            {
                Timer.gameObject.SetActive(false);
            }
        }

        // button click
        public void ShowCurrenciesWindow()
        {
            var shopPrefab = Prefabs.CurrenciesPacks;
            UIView.ShowWindow(shopPrefab);
        }
    }
}
