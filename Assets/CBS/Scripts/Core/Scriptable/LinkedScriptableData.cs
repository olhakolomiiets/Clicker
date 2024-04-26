using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "LinkedScriptableData", menuName = "CBS/Add new Linked Scriptable Data")]
    public class LinkedScriptableData : LinkedData<ScriptableObject>
    {
        public override string ResourcePath => "LinkedScriptableData";

        public override string EditorPath => "Assets/CBS_External/Resources";

        public override string EditorAssetName => "LinkedScriptableData.asset";

        public TScriptable GetScriptableData<TScriptable>(string id) where TScriptable : ScriptableObject
        {
            var asset = GetLinkedData(id);
            return (TScriptable)asset;
        }
    }
}
