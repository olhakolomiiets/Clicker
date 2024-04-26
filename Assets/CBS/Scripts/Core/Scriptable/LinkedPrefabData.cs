using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "LinkedPrefabData", menuName = "CBS/Add new Linked Prefab Data")]
    public class LinkedPrefabData : LinkedData<GameObject>
    {
        public override string ResourcePath => "LinkedPrefabData";

        public override string EditorPath => "Assets/CBS_External/Resources";

        public override string EditorAssetName => "LinkedPrefabData.asset";
    }
}
