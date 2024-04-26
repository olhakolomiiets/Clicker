using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "CalendarIcons", menuName = "CBS/Add new CalendarIcons Sprite pack")]
    public class CalendarIcons : IconsData
    {
        public override string ResourcePath => "CalendarIcons";

        public override string EditorPath => "Assets/CBS_External/Resources";

        public override string EditorAssetName => "CalendarIcons.asset";
    }
}
