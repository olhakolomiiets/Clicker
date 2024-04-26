
using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;

namespace CBS
{
    public class CBSEventsModule : CBSModule, IEventsModule
    {
        private IFabEvents FabEvents { get; set; }

        protected override void Init()
        {
            FabEvents = FabExecuter.Get<FabEvents>();
        }

        /// <summary>
        /// Get list of cbs event by active state or category
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void GetEvents(CBSGetEventsRequest request, Action<CBSGetEventsResult> result)
        {
            var activeOnly = request.ActiveOnly;
            var category = request.ByCategory;
            FabEvents.GetEventsList(activeOnly, category, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetEventsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetCBSEventsResult>();
                    var events = functionResult.Events;
                    result?.Invoke(new CBSGetEventsResult
                    {
                        IsSuccess = true,
                        Events = events
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetEventsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get detail information about cbs event by id
        /// </summary>
        /// <param name="eventID"></param>
        /// <param name="result"></param>
        public void GetEventByID(string eventID, Action<CBSGetEventResult> result)
        {
            FabEvents.GetEventByID(eventID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetEventResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetCBSEventResult>();
                    var eventInstance = functionResult.Event;
                    result?.Invoke(new CBSGetEventResult
                    {
                        IsSuccess = true,
                        Event = eventInstance
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetEventResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get number of active events. If category is null - get badge of all events
        /// </summary>
        /// <param name="result"></param>
        public void GetEventsBadge(string category, Action<CBSBadgeResult> result)
        {
            FabEvents.GetEventsBadge(category, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionBadgeResult>();
                    var badgeCount = functionResult.Count;

                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = true,
                        Count = badgeCount
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBadgeResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }
    }
}
