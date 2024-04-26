using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "CurrencyConfigData", menuName = "CBS/Add new Currency Config Data")]
    public class CurrencyConfigData : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/Core/CurrencyConfigData";

        public Sprite RMCurrencyIcon;
    }
}
