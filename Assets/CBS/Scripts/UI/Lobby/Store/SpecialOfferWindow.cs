using CBS.Models;
using CBS.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class SpecialOfferWindow : SpecialOfferFrame
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Descrition;
        [SerializeField]
        private Transform PriceRoot;

        private List<GameObject> CurrencyPool { get; set; }
        private IStore Store { get; set; }

        protected override void Awake()
        {
            base.Awake();
            Store = CBSModule.Get<CBSStoreModule>();
            CurrencyPool = new List<GameObject>();
        }

        public override void Load(CBSSpecialOffer offer)
        {
            base.Load(offer);
            DisplayName.text = Offer.DisplayName;
            Descrition.text = Offer.Description;

            SpawnPrices();
        }

        private void SpawnPrices()
        {
            // display prices
            foreach (var obj in CurrencyPool)
                obj.SetActive(false);

            var currencies = Offer.Prices;
            if (currencies == null)
                return;
            for (int i = 0; i < currencies.Count; i++)
            {
                var price = currencies.ElementAt(i);
                string key = price.Key;
                int val = (int)price.Value;

                if (i >= CurrencyPool.Count)
                {
                    var pricePrefab = Prefabs.PurchaseButton;
                    var priceUI = Instantiate(pricePrefab, PriceRoot);
                    CurrencyPool.Add(priceUI);
                }
                else
                {
                    CurrencyPool[i].SetActive(true);
                }
                var button = CurrencyPool[i].GetComponent<StorePurchaseButton>();
                button.Display(key, val, PurchaseRequest);
                if (Offer.HasDiscount(key))
                {
                    var discount = Offer.GetDiscount(key);
                    button.DrawDicount(discount);
                }
            }
        }

        private void PurchaseRequest(string code, int value)
        {
            var itemID = Offer.ItemID;
            var storeID = Offer.StoreID;
            if (code == PlayfabUtils.REAL_MONEY_CODE)
            {
                Store.PurchaseStoreItemWithRealMoney(itemID, storeID, OnPurchaseItemWithRM);
            }
            else
            {
                Store.PurchaseStoreItem(itemID, storeID, code, value, OnPurchaseItem);
            }
        }

        // events
        private void OnPurchaseItem(CBSPurchaseStoreItemResult result)
        {
            if (result.IsSuccess)
            {
                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = ItemTXTHandler.PurchaseTitle,
                    Body = ItemTXTHandler.PurchaseBody
                });
                CloseWindow();
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnPurchaseItemWithRM(CBSPurchaseStoreItemWithRMResult result)
        {
            if (result.IsSuccess)
            {
                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = ItemTXTHandler.PurchaseTitle,
                    Body = ItemTXTHandler.PurchaseBody
                });
                CloseWindow();
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        // button click
        public void CloseWindow()
        {
            gameObject.SetActive(false);
        }
    }
}
