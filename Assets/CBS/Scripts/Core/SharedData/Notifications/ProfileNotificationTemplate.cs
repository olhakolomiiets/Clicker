namespace CBS.Models
{
    public class ProfileNotificationTemplate
    {
        public string Title;
        public string Message;
        public RewardObject RewardObject;
        public CBSNotificationCustomData CustomData;
        public string RawCustomData;

        internal void PrepareCustomData()
        {
            if (CustomData == null)
                return;
            RawCustomData = JsonPlugin.ToJsonCompress(CustomData);
        }
    }
}