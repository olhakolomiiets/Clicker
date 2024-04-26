using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "ExamplesConfig", menuName = "CBS/Add new ExamplesConfig")]
    public class ExamplesConfig : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/Core/ExamplesConfig";

        public Sprite DefaultClanAvatar;
    }
}
