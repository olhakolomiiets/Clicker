using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CBS.UI
{
    public class LongClickTrigger : MonoBehaviour, IPointerUpHandler, IPointerDownHandler
    {
        [SerializeField]
        private float LongClickSeconds;

        public event Action OnLongClick;

        private float LastDownTime;
        private bool IsDown;

        public void OnPointerDown(PointerEventData data)
        {
            LastDownTime = Time.time;
            IsDown = true;
        }

        public void OnPointerUp(PointerEventData data)
        {
            IsDown = false;
        }

        private void LateUpdate()
        {
            if (!IsDown)
                return;
            if (Time.time > (LastDownTime + LongClickSeconds))
            {
                OnLongClick?.Invoke();
                IsDown = false;
            }
        }
    }
}
