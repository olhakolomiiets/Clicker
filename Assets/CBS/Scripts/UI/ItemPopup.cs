using CBS.Scriptable;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ItemPopup : MonoBehaviour
    {
        [SerializeField]
        private Text Title;
        [SerializeField]
        private Image ItemIcon;

        private Action CurrentAction { get; set; }
        private ItemsIcons Icons { get; set; }

        private void Awake()
        {
            Icons = CBSScriptable.Get<ItemsIcons>();
        }

        // setup popup information
        public void Setup(string itemID, string title, Action onOk)
        {
            Clear();
            Title.text = title;
            ItemIcon.sprite = Icons.GetSprite(itemID);
            CurrentAction = onOk;
        }

        // reset view
        private void Clear()
        {
            Title.text = string.Empty;
            CurrentAction = null;
            ItemIcon.sprite = null;
        }

        // button event
        public void OnOk()
        {
            CurrentAction?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
