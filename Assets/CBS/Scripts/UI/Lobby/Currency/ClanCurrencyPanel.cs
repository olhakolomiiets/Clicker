using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CBS.UI
{
    public class ClanCurrencyPanel : MonoBehaviour
    {
        [SerializeField]
        private CurrencyDisplayOptions DisplayOption;
        [SerializeField]
        private string[] SelectedCurrencies;

        private CurrencyPrefabs Prefabs { get; set; }
        private AuthData AuthData { get; set; }
        private IClanEconomy ClanEconomy { get; set; }
        private IProfile Profile { get; set; }

        private Dictionary<string, CurrencyItem> CurrenciesUI = new Dictionary<string, CurrencyItem>();

        private void Awake()
        {
            Prefabs = CBSScriptable.Get<CurrencyPrefabs>();
            AuthData = CBSScriptable.Get<AuthData>();
            ClanEconomy = CBSModule.Get<CBSClanModule>();
            Profile = CBSModule.Get<CBSProfileModule>();
        }

        private void OnEnable()
        {
            if (Profile.ExistInClan)
            {
                ClanEconomy.GetClanCurrencies(Profile.ClanID, OnCurrenciesGet);
            }
        }

        private void SpawnItems(Dictionary<string, CBSCurrency> currencies)
        {
            var itemPrefab = Prefabs.CurrencyItem;

            foreach (var currencyPair in currencies)
            {
                var currency = currencyPair.Value;
                var currencyCode = currency.Code;
                if (CurrenciesUI.ContainsKey(currencyCode))
                {
                    CurrenciesUI[currency.Code].Display(currency);
                }
                else
                {
                    var item = Instantiate(itemPrefab, transform);
                    var itemUI = item.GetComponent<CurrencyItem>();
                    itemUI.Display(currency);
                    CurrenciesUI[currency.Code] = itemUI;
                }
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
