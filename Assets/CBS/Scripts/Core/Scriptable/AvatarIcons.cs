using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "AvatarIcons", menuName = "CBS/Add new Avatars Sprite pack")]
    public class AvatarIcons : IconsData
    {
        public override string ResourcePath => "AvatarIcons";

        public override string EditorPath => "Assets/CBS_External/Resources";

        public override string EditorAssetName => "AvatarIcons.asset";
    }
}
