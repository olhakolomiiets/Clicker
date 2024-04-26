using CBS.Models;
using System;

namespace CBS
{
    public interface ICalendar
    {
        /// <summary>
        /// Notifies when a user has received a reward
        /// </summary>
        event Action<GrantRewardResult> OnRewardCollected;

        /// <summary>
        /// Notifies when calelendar was reseted
        /// </summary>
        event Action<CalendarInstance> OnCalendarReseted;

        /// <summary>
        /// Notifies when calelendar was granted
        /// </summary>
        event Action<CalendarInstance> OnCalendarGranted;

        /// <summary>
        /// Notifies when calelendar was purchased
        /// </summary>
        event Action<CalendarInstance> OnCalendarPurchased;

        /// <summary>
        /// Get list of all available calendars for profile
        /// </summary>
        /// <param name="result"></param>
        void GetAllAvailableCalendars(Action<CBSGetAllCalendarsResult> result);

        /// <summary>
        /// Get information about the status of the calendar by id. Also get a list of all calendar rewards.
        /// </summary>
        /// <param name="result"></param>
        void GetCalendarByID(string calendarID, Action<CBSGetCalendarResult> result);

        /// <summary>
        /// Pickup calendar reward.
        /// </summary>
        /// <param name="result"></param>
        void PickupReward(string calendarID, int positionIndex, Action<CBSPickupCalendarReward> result);

        /// <summary>
        /// Reset caledar states for current profile.
        /// </summary>
        /// <param name="result"></param>
        void ResetCalendar(string calendarID, Action<CBSResetCalendarResult> result);

        /// <summary>
        /// Get the amount of rewards profile can pickup today
        /// </summary>
        /// <param name="result"></param>
        void GetCalendarBadge(Action<CBSBadgeResult> result);

        /// <summary>
        /// Grant calendar instance to profile. Only work when "Activation" equal "BY_PURCHASE"
        /// </summary>
        /// <param name="result"></param>
        void GrantCalendar(string calendarID, Action<CBSGrantCalendarResult> result);

        /// <summary>
        /// Purchase calendar with currencies.
        /// </summary>
        /// <param name="calendarID"></param>
        /// <param name="result"></param>
        void PurchaseCalendar(string calendarID, Action<CBSPurchaseCalendarResult> result);

        /// <summary>
        /// Purchase calendar with real money.
        /// </summary>
        /// <param name="calendarID"></param>
        /// <param name="result"></param>
        void PurchaseCalendarWithRM(string calendarID, Action<CBSPurchaseCalendarWithRMResult> result);
    }
}
