using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class SpecialOfferFrame : MonoBehaviour
    {
        [SerializeField]
        private Image Icon;
        [SerializeField]
        private Text Label;

        protected CBSSpecialOffer Offer { get; private set; }
        protected RectTransform Rect { get; private set; }
        protected StorePrefabs Prefabs { get; private set; }

        protected virtual void Awake()
        {
            Rect = GetComponent<RectTransform>();
            Prefabs = CBSScriptable.Get<StorePrefabs>();
        }

        public virtual void Load(CBSSpecialOffer offer)
        {
            Offer = offer;
            Icon.sprite = offer.GetSprite();
            Label.text = string.Empty;
            Rect.SetAsLastSibling();
        }

        private void OnDisable()
        {
            Offer = null;
        }

        private void DisplayTimer()
        {
            if (Offer == null)
                return;
            var endDate = Offer.OfferEndDate;
            if (endDate != null)
            {
                var endDateValid = endDate.GetValueOrDefault();
                var utcDate = DateTime.UtcNow;
                var span = endDateValid.Subtract(utcDate);
                if (span.Ticks > 0)
                {
                    var totalDays = (int)span.TotalDays;
                    var timeString = span.ToString(DateUtils.StoreTimerFormat);
                    var sBuilder = new StringBuilder();
                    sBuilder.Append(totalDays > 0 ? totalDays + " Days " : string.Empty);
                    sBuilder.Append(timeString);
                    Label.text = sBuilder.ToString();
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }

        private void LateUpdate()
        {
            DisplayTimer();
        }

        // button click
        public void ShowOfferWindow()
        {
            var offerPrefab = Prefabs.SpecialOfferWindow;
            var uiObject = UIView.ShowWindow(offerPrefab);
            uiObject.GetComponent<SpecialOfferWindow>().Load(Offer);
        }
    }
}
