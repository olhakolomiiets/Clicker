using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CBS.UI
{
    public class CurrencyPanel : MonoBehaviour
    {
        [SerializeField]
        private CurrencyDisplayOptions DisplayOption;
        [SerializeField]
        private string[] SelectedCurrencies;

        private CurrencyPrefabs Prefabs { get; set; }
        private AuthData AuthData { get; set; }
        private ICurrency Currencies { get; set; }

        private List<CurrencyItem> CurrenciesUI = new List<CurrencyItem>();

        private void Start()
        {
            Prefabs = CBSScriptable.Get<CurrencyPrefabs>();
            AuthData = CBSScriptable.Get<AuthData>();
            Currencies = CBSModule.Get<CBSCurrencyModule>();

            Currencies.OnCurrencyUpdated += OnCurrencyUpdated;

            // check cache currencies
            if (AuthData.PreloadCurrency)
            {
                var correctedCurrencies = GetSelectedCurrencies(Currencies.CacheCurrencies);
                SpawnItems(correctedCurrencies);
            }
            else
            {
                Currencies.GetProfileCurrencies(OnCurrenciesGet);
            }
        }

        private void OnDestroy()
        {
            Currencies.OnCurrencyUpdated -= OnCurrencyUpdated;
        }

        private void SpawnItems(Dictionary<string, CBSCurrency> currencies)
        {
            var itemPrefab = Prefabs.CurrencyItem;

            foreach (var currencyPair in currencies)
            {
                var currency = currencyPair.Value;
                var item = Instantiate(itemPrefab, transform);
                var itemUI = item.GetComponent<CurrencyItem>();
                itemUI.Display(currency);
                CurrenciesUI.Add(itemUI);
            }
        }

        // events
        private void OnCurrenciesGet(CBSGetCurrenciesResult result)
        {
            if (result.IsSuccess)
            {
                var correctedCurrencies = GetSelectedCurrencies(result.Currencies);
                SpawnItems(correctedCurrencies);
            }
        }

        private void OnCurrencyUpdated(CBSCurrency currency)
        {
            foreach (var ui in CurrenciesUI)
            {
                ui.UpdateCurrency(currency);
            }
        }

        private Dictionary<string, CBSCurrency> GetSelectedCurrencies(Dictionary<string, CBSCurrency> inputCurrencies)
        {
            if (DisplayOption == CurrencyDisplayOptions.SELECTED)
            {
                return inputCurrencies.Where(x => SelectedCurrencies.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            }
            return inputCurrencies;
        }
    }
}
