using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class BattlePassTab : MonoBehaviour
    {
        public BattlePassTitle TabType;

        public Action<BattlePassTab> OnTabSelected;

        public bool IsActive => Toggle.isOn;

        private Toggle toggle;
        public Toggle Toggle
        {
            get
            {
                if (toggle == null)
                {
                    toggle = gameObject.GetComponent<Toggle>();
                }
                return toggle;
            }
        }

        private void Start()
        {
            Toggle.onValueChanged.AddListener(OnToggleChanged);
        }

        private void OnDestroy()
        {
            Toggle.onValueChanged.RemoveListener(OnToggleChanged);
        }

        public void Activate()
        {
            Toggle.SetIsOnWithoutNotify(true);
        }

        // events
        private void OnToggleChanged(bool val)
        {
            if (val)
            {
                OnTabSelected?.Invoke(this);
            }
        }
    }
}
