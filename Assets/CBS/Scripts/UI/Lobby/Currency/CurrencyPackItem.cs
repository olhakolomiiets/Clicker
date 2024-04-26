using CBS.Scriptable;
using CBS.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class CurrencyPackItem : MonoBehaviour
    {
        [SerializeField]
        private Text Title;
        [SerializeField]
        private Text Description;
        [SerializeField]
        private Image Icon;
        [SerializeField]
        private Text Price;

        [SerializeField]
        private Transform CurrencySlots;

        private CBSCurrencyPack Pack { get; set; }
        private CurrencyPrefabs Prefabs { get; set; }
        private ICurrency Currency { get; set; }
        private ICBSInAppPurchase CBSPurchase { get; set; }

        private void Awake()
        {
            Prefabs = CBSScriptable.Get<CurrencyPrefabs>();
            Currency = CBSModule.Get<CBSCurrencyModule>();
            CBSPurchase = CBSModule.Get<CBSInAppPurchaseModule>();
        }

        public void Display(CBSCurrencyPack pack)
        {
            Pack = pack;
            Title.text = Pack.DisplayName;
            Description.text = Pack.Description;
            Icon.sprite = Pack.GetSprite();
            Price.text = Pack.PriceTitle;

            foreach (var currency in Pack.Currencies)
            {
                var slotPrefab = Prefabs.CurrencySlot;
                var slot = Instantiate(slotPrefab, CurrencySlots);
                slot.GetComponent<CurrencySlot>().Display(currency.Value);
            }
        }

        // button events
        public void PurchasePack()
        {
            Currency.PurchasePackWithRealMoney(Pack.ID, result =>
            {
                if (!result.IsSuccess)
                {
                    new PopupViewer().ShowFabError(result.Error);
                }
                else
                {
                    new PopupViewer().ShowSimplePopup(new PopupRequest
                    {
                        Title = ItemTXTHandler.PurchaseTitle,
                        Body = ItemTXTHandler.PurchaseBody
                    });
                }
            });
        }
    }
}
