using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "EventsPrefabs", menuName = "CBS/Add new Events Prefabs")]
    public class EventsPrefabs : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/EventsPrefabs";

        public GameObject EventWindow;
        public GameObject EventSlot;
    }
}
