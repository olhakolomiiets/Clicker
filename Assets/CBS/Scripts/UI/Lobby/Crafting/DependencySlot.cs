using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class DependencySlot : MonoBehaviour, IScrollableItem<ItemDependencyState>
    {
        [SerializeField]
        private Text NeedTitle;
        [SerializeField]
        private Text PresentTitle;
        [SerializeField]
        private Image Icon;
        [SerializeField]
        private GameObject CheckMark;
        [SerializeField]
        private GameObject Close;

        private CurrencyIcons CurrencyIcons { get; set; }
        private ItemsIcons ItemsIcons { get; set; }

        private void Awake()
        {
            CurrencyIcons = CBSScriptable.Get<CurrencyIcons>();
            ItemsIcons = CBSScriptable.Get<ItemsIcons>();
        }

        public void Display(ItemDependencyState item)
        {
            var isEnough = item.IsValid();
            CheckMark.SetActive(isEnough);
            Close.SetActive(!isEnough);
            var type = item.Type;
            var id = item.ID;
            var need = item.NeedCount;
            var present = item.PresentCount;

            var iconSprite = type == ItemDependencyType.ITEM ? ItemsIcons.GetSprite(id) : CurrencyIcons.GetSprite(id);
            Icon.sprite = iconSprite;
            NeedTitle.text = need.ToString();
            PresentTitle.text = present.ToString();
            PresentTitle.color = isEnough ? Color.green : Color.red;
        }
    }
}
