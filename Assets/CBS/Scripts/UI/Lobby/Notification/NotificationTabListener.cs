using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class NotificationTabListener : MonoBehaviour
    {
        public event Action<NotificationMenu> OnTabSelected;

        [SerializeField]
        private GameObject[] AllTabs;

        public NotificationMenu ActiveTab { get; private set; }

        private void Awake()
        {
            foreach (var tab in AllTabs)
            {
                tab.GetComponent<Toggle>().onValueChanged.AddListener(OnToggleSelected);
            }
        }

        private void OnDestroy()
        {
            foreach (var tab in AllTabs)
            {
                tab.GetComponent<Toggle>().onValueChanged.RemoveListener(OnToggleSelected);
            }
        }

        private void OnToggleSelected(bool val)
        {
            if (val)
            {
                var activeTab = AllTabs.FirstOrDefault(x => x.GetComponent<Toggle>().isOn);
                ActiveTab = activeTab.GetComponent<NotificationTab>().GetTabType();
                OnTabSelected?.Invoke(ActiveTab);
            }
        }
    }
}