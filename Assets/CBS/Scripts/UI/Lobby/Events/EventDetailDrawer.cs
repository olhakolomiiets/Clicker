using CBS.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class EventDetailDrawer : MonoBehaviour
    {
        [SerializeField]
        private Image Icon;
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Description;

        private Action BackAction { get; set; }

        public void Draw(CBSEvent eventInstance, Action backAction)
        {
            BackAction = backAction;

            var eventIcon = eventInstance.GetIconSprite();
            Icon.sprite = eventIcon;
            DisplayName.text = eventInstance.DisplayName;
            Description.text = eventInstance.Description;
        }

        public void OnBackHandler()
        {
            BackAction?.Invoke();
        }
    }
}

