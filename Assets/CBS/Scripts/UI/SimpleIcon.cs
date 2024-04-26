using CBS.Scriptable;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class SimpleIcon : MonoBehaviour
    {
        [SerializeField]
        private Image Icon;
        [SerializeField]
        private Text Value;

        private ItemsIcons ItemsIcons { get; set; }
        private CurrencyIcons CurrencyIcons { get; set; }

        private bool IsInited { get; set; }

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            ItemsIcons = CBSScriptable.Get<ItemsIcons>();
            CurrencyIcons = CBSScriptable.Get<CurrencyIcons>();
            IsInited = true;
        }

        public void DrawItem(string id)
        {
            if (!IsInited)
                Init();
            Icon.gameObject.SetActive(true);
            Icon.sprite = ItemsIcons.GetSprite(id);
        }

        public void DrawCurrency(string id)
        {
            if (!IsInited)
                Init();
            Icon.gameObject.SetActive(true);
            Icon.sprite = CurrencyIcons.GetSprite(id);
        }

        public void DrawValue(string val)
        {
            Value.gameObject.SetActive(true);
            Value.text = val;
        }

        public void HideValue()
        {
            Value.gameObject.SetActive(false);
        }

        public void HideIcon()
        {
            Icon.gameObject.SetActive(false);
        }
    }
}
