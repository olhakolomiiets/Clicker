using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Button))]
    public class StorePurchaseButton : PurchaseButton
    {
        [SerializeField]
        private GameObject DiscountObject;
        [SerializeField]
        private Text DiscountLabel;

        public override void Display(string code, int value, Action<string, int> onPress)
        {
            base.Display(code, value, onPress);
            DiscountObject.SetActive(false);
            var priceRect = CurrencyValue.rectTransform;
            var priceSize = priceRect.sizeDelta;
            priceSize.x = CurrencyValue.preferredWidth;
            priceRect.sizeDelta = priceSize;
            SetContentAligment(TextAnchor.MiddleCenter);
        }

        public void DrawDicount(int discount)
        {
            DiscountObject.SetActive(true);
            DiscountLabel.text = TextUtils.GetDiscountText(discount);
            SetContentAligment(TextAnchor.MiddleRight);
        }

        public void SetPurchaseActivity(bool activity)
        {
            Button.interactable = activity;
        }

        private void SetContentAligment(TextAnchor anchor)
        {
            var layoutGroup = GetComponent<HorizontalOrVerticalLayoutGroup>();
            layoutGroup.childAlignment = anchor;
        }
    }
}
