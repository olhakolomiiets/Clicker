using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "CraftPrefabs", menuName = "CBS/Add new Craft Prefabs")]
    public class CraftPrefabs : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/CraftPrefabs";

        public GameObject CraftWindow;
        public GameObject DependencySlot;
        public GameObject RecipeSlot;
    }
}
