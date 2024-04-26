using CBS.Models;
using System;

namespace CBS.UI
{
    public class NotificationSlotRequest
    {
        public CBSNotification Notification;
        public CBSNotification Active;
        public Action<NotificationSlot> SelectAction;
    }
}
