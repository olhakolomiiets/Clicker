using CBS.Models;
using System;

namespace CBS
{
    public interface IEventsModule
    {
        /// <summary>
        /// Get list of cbs event by active state or category
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void GetEvents(CBSGetEventsRequest request, Action<CBSGetEventsResult> result);

        /// <summary>
        /// Get detail information about cbs event by id
        /// </summary>
        /// <param name="eventID"></param>
        /// <param name="result"></param>
        void GetEventByID(string eventID, Action<CBSGetEventResult> result);

        /// <summary>
        /// Get number of active events. If category is null - get badge of all events
        /// </summary>
        /// <param name="result"></param>
        void GetEventsBadge(string category, Action<CBSBadgeResult> result);
    }
}

