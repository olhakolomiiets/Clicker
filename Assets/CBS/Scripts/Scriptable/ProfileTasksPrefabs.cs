using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "ProfileTasksPrefabs", menuName = "CBS/Add new Profile Tasks Prefabs")]
    public class ProfileTasksPrefabs : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/ProfileTasksPrefabs";

        public GameObject ProfileTasksWindow;
        public GameObject TaskSlot;
    }
}
