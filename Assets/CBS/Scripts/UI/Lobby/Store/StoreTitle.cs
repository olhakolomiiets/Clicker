using CBS.Core;
using CBS.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Toggle))]
    public class StoreTitle : MonoBehaviour, IScrollableItem<CBSStoreTitle>
    {
        [SerializeField]
        private Color ActiveColor;
        [SerializeField]
        private Color DisableColor;
        [SerializeField]
        private Text Title;

        private Toggle Toggle { get; set; }
        private Action<CBSStoreTitle> ChangeAction { get; set; }
        private CBSStoreTitle Store { get; set; }
        private RectTransform RootRect { get; set; }

        private void Awake()
        {
            Toggle = GetComponent<Toggle>();
            Toggle.onValueChanged.AddListener(OnToggleChange);
            RootRect = gameObject.GetComponent<RectTransform>();
        }

        private void OnDestroy()
        {
            Toggle.onValueChanged.RemoveListener(OnToggleChange);
        }

        public void Display(CBSStoreTitle store)
        {
            Store = store;
            Title.text = store.DisplayName;
            Resize();
            UpdateLabelColor();
        }

        public void SetChangeAction(Action<CBSStoreTitle> changeAction)
        {
            ChangeAction = changeAction;
        }

        public void SetGroup(ToggleGroup group)
        {
            Toggle.group = group;
        }

        public void Activate()
        {
            if (Toggle.isOn)
            {
                Toggle.isOn = false;
            }
            Toggle.isOn = true;
        }

        private void Resize()
        {
            var size = RootRect.sizeDelta;
            size.x = Title.preferredWidth;
            RootRect.sizeDelta = size;
        }

        private void UpdateLabelColor()
        {
            var newColor = Toggle.isOn ? ActiveColor : DisableColor;
            Title.color = newColor;
        }

        // events
        private void OnToggleChange(bool val)
        {
            if (val)
            {
                ChangeAction?.Invoke(Store);
            }
            UpdateLabelColor();
        }
    }
}
