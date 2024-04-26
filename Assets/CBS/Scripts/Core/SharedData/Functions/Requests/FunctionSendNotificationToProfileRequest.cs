namespace CBS.Models
{
    public class FunctionSendNotificationToProfileRequest : FunctionBaseRequest
    {
        public string ToProfileID;
        public ProfileNotificationTemplate NotificationTemplate;
    }
}