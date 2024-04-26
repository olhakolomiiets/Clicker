using CBS.Core;
using CBS.Models;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Button))]
    public class EventSlot : MonoBehaviour, IScrollableItem<CBSEvent>
    {
        [SerializeField]
        private Image BackgroundIcon;
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Date;
        [SerializeField]
        private GameObject ActiveBack;
        [SerializeField]
        private GameObject InactiveBack;
        [SerializeField]
        private GameObject Locker;
        [SerializeField]
        private Sprite DefaultSprite;

        private CBSEvent Event { get; set; }
        private Action<CBSEvent> SelectAction { get; set; }


        public void Display(CBSEvent data)
        {
            Event = data;
            DisplayName.text = data.DisplayName;
            var isActive = data.IsRunning;
            ActiveBack.SetActive(isActive);
            InactiveBack.SetActive(!isActive);
            Locker.SetActive(!isActive);
            var eventSprite = data.GetBackgroundSprite();
            BackgroundIcon.sprite = eventSprite == null ? DefaultSprite : eventSprite;
        }

        public void SetSelectAction(Action<CBSEvent> selectAction)
        {
            SelectAction = selectAction;
        }

        private void DisplayDate()
        {
            var isActive = Event.IsRunning;
            if (isActive)
            {
                var endDate = Event.EndDate;
                if (endDate == null)
                    Date.text = string.Empty;
                else
                    Date.text = EventsUtils.GetEndDateNotification(endDate.GetValueOrDefault());
            }
            else
            {
                var nextDate = Event.NextRunTime;
                if (nextDate == null)
                    Date.text = string.Empty;
                else
                    Date.text = EventsUtils.GetNextDateNotification(nextDate.GetValueOrDefault());
            }
        }

        private void LateUpdate()
        {
            if (Event == null)
                return;
            DisplayDate();
        }

        public void OnClickView()
        {
            SelectAction?.Invoke(Event);
        }
    }
}
