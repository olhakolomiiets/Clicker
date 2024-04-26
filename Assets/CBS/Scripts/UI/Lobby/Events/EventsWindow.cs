using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class EventsWindow : MonoBehaviour
    {
        [SerializeField]
        private EventsScroller Scroller;
        [SerializeField]
        private EventDetailDrawer EventDrawer;

        [Header("Display options")]

        [SerializeField]
        private bool ActiveOnly;
        [SerializeField]
        private string ByCategory;

        private IEventsModule Events { get; set; }
        private EventsPrefabs Prefabs { get; set; }

        private void Awake()
        {
            Events = CBSModule.Get<CBSEventsModule>();
            Prefabs = CBSScriptable.Get<EventsPrefabs>();
        }

        private void OnEnable()
        {
            Scroller.gameObject.SetActive(true);
            EventDrawer.gameObject.SetActive(false);
            GetEventsList();
        }

        private void DisplayEvents(List<CBSEvent> events)
        {
            Scroller.HideAll();
            var uiPrefab = Prefabs.EventSlot;
            var uiList = Scroller.Spawn(uiPrefab, events);
            foreach (var ui in uiList)
                ui.GetComponent<EventSlot>().SetSelectAction(OnViewEvent);
        }

        private void GetEventsList()
        {
            Events.GetEvents(new CBSGetEventsRequest
            {
                ActiveOnly = ActiveOnly,
                ByCategory = ByCategory
            }, OnGetEvents);
        }

        // events
        private void OnGetEvents(CBSGetEventsResult result)
        {
            if (result.IsSuccess)
            {
                var events = result.Events;
                DisplayEvents(events);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnViewEvent(CBSEvent selectedEvent)
        {
            Scroller.gameObject.SetActive(false);
            EventDrawer.gameObject.SetActive(true);
            EventDrawer.Draw(selectedEvent, OnBackFromDetail);
        }

        private void OnBackFromDetail()
        {
            Scroller.gameObject.SetActive(true);
            EventDrawer.gameObject.SetActive(false);
        }
    }
}

