using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "StickersIcons", menuName = "CBS/Add new Sticker Sprite pack")]
    public class StickersIcons : IconsData
    {
        public override string ResourcePath => "StickersIcons";

        public override string EditorPath => "Assets/CBS_External/Resources";

        public override string EditorAssetName => "StickersIcons.asset";
    }
}
