

namespace CBS.Models
{
    public class FunctionGetNotificationsRequest : FunctionBaseRequest
    {
        public NotificationRequest Request;
        public int MaxCount;
        public string Category;
    }
}