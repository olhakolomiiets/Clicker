using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "CalendarPrefabs", menuName = "CBS/Add new Calendar Prefabs")]
    public class CalendarPrefabs : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/CalendarPrefabs";

        public GameObject CalendarWindow;
        public GameObject CalendarTitle;
        public GameObject CalendarSlot;
    }
}
