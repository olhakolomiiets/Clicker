using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class StoreItem : MonoBehaviour, IScrollableItem<CBSStoreItem>
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Description;
        [SerializeField]
        private Text Notification;
        [SerializeField]
        private Image Icon;
        [SerializeField]
        private Transform PriceRoot;

        private List<GameObject> CurrencyPool { get; set; }
        private CBSStoreItem Item { get; set; }
        private StorePrefabs Prefabs { get; set; }

        private IStore Store { get; set; }
        private DateTime? LimitResetDate { get; set; }

        private void Awake()
        {
            CurrencyPool = new List<GameObject>();
            Prefabs = CBSScriptable.Get<StorePrefabs>();
            Store = CBSModule.Get<CBSStoreModule>();
        }

        private void OnDisable()
        {
            LimitResetDate = null;
            ResetButtonsActivity();
        }

        public void Display(CBSStoreItem item)
        {
            Item = item;
            DisplayName.text = item.DisplayName;
            Description.text = item.Description;
            Notification.text = string.Empty;
            var iconSprite = item.GetSprite();
            Icon.sprite = iconSprite;
            SpawnPrices();
            var hasLimit = Item.HasQuantityLimit;
            if (hasLimit)
                DisplayNotification(Item.Limitation);
        }

        private void DisplayNotification(StoreLimitationInfo limitInfo)
        {
            if (limitInfo != null)
            {
                var leftCount = limitInfo.LeftQuantity;
                if (leftCount <= 0)
                {
                    LimitResetDate = limitInfo.ResetLimitDate;
                }
                else
                {
                    var period = limitInfo.LimitPeriod;
                    Notification.text = StoreTXTHandler.GetLimitationNotification(period, leftCount);
                }
            }
            CheckButtonsActivity(limitInfo);
        }

        private void SpawnPrices()
        {
            // display prices
            foreach (var obj in CurrencyPool)
                obj.SetActive(false);

            var prices = Item.Prices;
            if (prices == null)
                return;
            for (int i = 0; i < prices.Count; i++)
            {
                var price = prices.ElementAt(i);
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
                if (Item.HasDiscount(key))
                {
                    var discount = Item.GetDiscount(key);
                    button.DrawDicount(discount);
                }
            }
        }

        private void ResetButtonsActivity()
        {
            foreach (var purchaseButton in CurrencyPool)
            {
                var button = purchaseButton.GetComponent<StorePurchaseButton>();
                button.SetPurchaseActivity(true);
            }
        }

        private void CheckButtonsActivity(StoreLimitationInfo limitInfo)
        {
            if (limitInfo == null)
            {
                foreach (var purchaseButton in CurrencyPool)
                {
                    var button = purchaseButton.GetComponent<StorePurchaseButton>();
                    button.SetPurchaseActivity(true);
                }
            }
            else
            {
                foreach (var purchaseButton in CurrencyPool)
                {
                    var button = purchaseButton.GetComponent<StorePurchaseButton>();
                    button.SetPurchaseActivity(limitInfo.LeftQuantity > 0);
                }
            }
        }

        private void PurchaseRequest(string code, int value)
        {
            var itemID = Item.ItemID;
            var storeID = Item.StoreID;
            if (code == PlayfabUtils.REAL_MONEY_CODE)
            {
                Store.PurchaseStoreItemWithRealMoney(itemID, storeID, OnPurchaseItemWithRM);
            }
            else
            {
                Store.PurchaseStoreItem(itemID, storeID, code, value, OnPurchaseItem);
            }
        }

        private void LateUpdate()
        {
            DisplayLimitationTimer();
        }

        private void DisplayLimitationTimer()
        {
            if (LimitResetDate == null)
                return;
            var period = Item.Limitation.LimitPeriod;
            Notification.text = StoreTXTHandler.GetNextResetLimitNotification((DateTime)LimitResetDate, period);
        }

        // events
        private void OnPurchaseItem(CBSPurchaseStoreItemResult result)
        {
            if (result.IsSuccess)
            {
                var limitation = result.LimitationInfo;
                DisplayNotification(limitation);

                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = ItemTXTHandler.PurchaseTitle,
                    Body = ItemTXTHandler.PurchaseBody
                });
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
                var limitation = result.LimitationInfo;
                DisplayNotification(limitation);

                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = ItemTXTHandler.PurchaseTitle,
                    Body = ItemTXTHandler.PurchaseBody
                });
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
