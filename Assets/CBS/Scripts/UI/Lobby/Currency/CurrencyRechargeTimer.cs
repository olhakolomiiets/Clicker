using CBS.Models;
using CBS.Utils;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class CurrencyRechargeTimer : MonoBehaviour
    {
        [SerializeField]
        private Text TimerLabel;

        private CBSCurrency Currency { get; set; }

        private void OnDisable()
        {
            StopTimer();
        }

        public void StartTimer(CBSCurrency currency)
        {
            Currency = currency;
            if (!Currency.Rechargeable)
                return;
            StopAllCoroutines();
            StartCoroutine(OnTick());
        }

        private IEnumerator OnTick()
        {
            while (true)
            {
                var leftSeconds = Currency.GetSecondsToNextRecharge();
                if (leftSeconds <= 0)
                {
                    StopTimer();
                    //CBSModule.Get<CBSCurrencies>().ChangeRequest(Currency.Code);
                    gameObject.SetActive(false);
                    yield break;
                }
                else
                {
                    var timeSpan = TimeSpan.FromSeconds(leftSeconds);
                    var days = timeSpan.Days;
                    var timeString = timeSpan.ToString(DateUtils.CurrencyTimerFormat);
                    TimerLabel.text = timeString;
                }
                yield return new WaitForSeconds(1);
            }
        }

        public void StopTimer()
        {
            TimerLabel.text = DateUtils.DefaultTimer;
            StopAllCoroutines();
        }
    }
}
