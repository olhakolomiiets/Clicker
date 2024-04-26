using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "NotificationPrefabs", menuName = "CBS/Add new Notification Prefabs")]
    public class NotificationPrefabs : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/NotificationPrefabs";

        public GameObject NotificationWindow;
        public GameObject NotificationTitle;
    }
}
