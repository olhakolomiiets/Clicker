using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "StoreIcons", menuName = "CBS/Add new StoreIcons Sprite pack")]
    public class StoreIcons : IconsData
    {
        public override string ResourcePath => "StoreIcons";

        public override string EditorPath => "Assets/CBS_External/Resources";

        public override string EditorAssetName => "StoreIcons.asset";
    }
}
