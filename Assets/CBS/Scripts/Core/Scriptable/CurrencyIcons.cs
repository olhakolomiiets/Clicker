using CBS.Utils;
using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "CurrencyIcons", menuName = "CBS/Add new Currency Sprite pack")]
    public class CurrencyIcons : IconsData
    {
        public override string ResourcePath => "CurrencyIcons";

        public override string EditorPath => "Assets/CBS_External/Resources";

        public override string EditorAssetName => "CurrencyIcons.asset";

        public override Sprite GetSprite(string id)
        {
            if (id == PlayfabUtils.REAL_MONEY_CODE)
            {
                var currencyConfig = Get<CurrencyConfigData>();
                return currencyConfig.RMCurrencyIcon;
            }
            return base.GetSprite(id);
        }
    }
}
