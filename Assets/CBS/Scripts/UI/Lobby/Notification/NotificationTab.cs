using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Toggle))]
    public class NotificationTab : MonoBehaviour
    {
        [SerializeField]
        private NotificationMenu TabType;
        [SerializeField]
        private Text Title;
        [SerializeField]
        private Color ActiveColor;
        [SerializeField]
        private Color DisableColor;

        private Toggle Toggle { get; set; }

        private void Awake()
        {
            Toggle = GetComponent<Toggle>();
            Toggle.onValueChanged.AddListener(OnStateChange);
        }

        private void OnDestroy()
        {
            Toggle.onValueChanged.RemoveListener(OnStateChange);
        }

        private void OnEnable()
        {
            DrawState(Toggle.isOn);
        }

        private void DrawState(bool state)
        {
            var stateColor = state ? ActiveColor : DisableColor;
            Title.color = stateColor;
        }

        public NotificationMenu GetTabType()
        {
            return TabType;
        }

        private void OnStateChange(bool state)
        {
            DrawState(state);
        }
    }
}
