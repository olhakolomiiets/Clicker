using CBS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CBS.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class DraggableInventorySlot : InventorySlot, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform RectTransform { get; set; }
        private Transform CanvasParent { get; set; }
        private int LastBindIndex { get; set; }
        private Transform LastParent { get; set; }

        public Action OnStartDrag;

        protected override void Awake()
        {
            base.Awake();
            RectTransform = gameObject.GetComponent<RectTransform>();
            CanvasParent = gameObject.GetComponentInParent<Canvas>().transform;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (LastParent == null)
            {
                LastBindIndex = RectTransform.GetSiblingIndex();
                LastParent = transform.parent;
            }
            SetParent(CanvasParent);
            OnStartDrag?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var container = GetDropContainer(eventData);
            if (container != null)
            {
                container.OnDropItem(gameObject);
            }
            else
            {
                ReturnToInitialPosition();
            }
        }

        public void ReturnToInitialPosition()
        {
            SetParent(LastParent);
            RectTransform.SetSiblingIndex(LastBindIndex);
        }

        public void SetParent(Transform parent)
        {
            transform.SetParent(parent);
        }

        private IDropContainer GetDropContainer(PointerEventData eventData)
        {
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            var container = results.FirstOrDefault(x => x.gameObject != null && x.gameObject.GetComponent<IDropContainer>() != null).gameObject;
            if (container != null)
                return container.GetComponent<IDropContainer>();
            return null;
        }
    }
}
