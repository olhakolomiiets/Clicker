using CBS.Models;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "IAPConfig", menuName = "CBS/Add new IAP Config")]
    public class IAPConfig : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/Core/IAPConfig";

        public bool EnableIAP;

        public List<CBSExternalProduct> ExternalIDs;
    }
}
