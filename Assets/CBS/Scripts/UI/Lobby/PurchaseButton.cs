using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Button))]
    public class PurchaseButton : MonoBehaviour
    {
        [SerializeField]
        private Image CurrencyIcon;
        [SerializeField]
        protected Text CurrencyValue;

        private Action<string, int> OnPress { get; set; }

        private string Code { get; set; }
        private int Value { get; set; }

        protected Button Button { get; set; }
        private CurrencyIcons CurrencyIcons { get; set; }

        private bool IsInited { get; set; }

        private void Awake()
        {
            Init();
        }

        private void OnDestroy()
        {
            Button.onClick.RemoveAllListeners();
        }

        public void Init()
        {
            if (IsInited)
                return;
            Button = gameObject.GetComponent<Button>();
            Button.onClick.AddListener(OnClick);
            CurrencyIcons = CBSScriptable.Get<CurrencyIcons>();
            IsInited = true;
        }

        public virtual void Display(string code, int value, Action<string, int> onPress)
        {
            Code = code;
            Value = value;
            OnPress = onPress;
            // display icon
            CurrencyIcon.sprite = CurrencyIcons.GetSprite(code);
            // display value
            CurrencyValue.text = PlayfabUtils.CurrencyValueToString(Code, Value);
        }

        public virtual void Display(CBSPrice price, Action<string, int> onPress)
        {
            Display(price.CurrencyID, price.CurrencyValue, onPress);
        }

        public void SetActivity(bool activity)
        {
            Button.interactable = activity;
        }

        private void OnClick()
        {
            OnPress?.Invoke(Code, Value);
        }
    }
}
