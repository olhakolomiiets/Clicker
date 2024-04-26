using CBS.Core;
using System;


namespace CBS.Models
{
    public class CBSNotification : ICustomData<CBSNotificationCustomData>
    {
        public string ID;
        public string InstanceID;
        public DateTime CreatedDate;
        public string Title;
        public string Message;
        public string Category;
        public string ExternalURL;
        public bool Read;
        public NotificationTarget Target;

        public bool HasReward;
        public bool VisibleForNewPlayer;
        public bool Rewarded;
        public RewardObject Reward;

        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }

        public bool CompressCustomData => true;

        public T GetCustomData<T>() where T : CBSNotificationCustomData
        {
            if (CompressCustomData)
                return JsonPlugin.FromJsonDecompress<T>(CustomRawData);
            else
                return JsonPlugin.FromJson<T>(CustomRawData);
        }

        public bool ReadAndRewarded()
        {
            if (HasReward)
            {
                return Read && Rewarded;
            }
            else
            {
                return Read;
            }
        }

        public static CBSNotification FromRewardDelivery(RewardDelivery delivery, RewardObject reward)
        {
            return new CBSNotification
            {
                ID = Guid.NewGuid().ToString(),
                Title = delivery.NotificationTitle,
                Message = delivery.NotificationMessage,
                HasReward = true,
                Reward = reward
            };
        }

        public static CBSNotification FromProfileTemplate(ProfileNotificationTemplate notificationTemplate)
        {
            return new CBSNotification
            {
                ID = Guid.NewGuid().ToString(),
                Title = notificationTemplate.Title,
                Message = notificationTemplate.Message,
                HasReward = notificationTemplate.RewardObject != null,
                Reward = notificationTemplate.RewardObject,
                CustomRawData = notificationTemplate.RawCustomData
            };
        }
    }
}

