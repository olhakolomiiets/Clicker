using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlanetBuilder.Messages.Tutorial
{
    public class TutorialTargetClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public event Action OnClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClicked?.Invoke();
        }
    }
}
