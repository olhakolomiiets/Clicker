using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class FriendUI : MonoBehaviour, IScrollableItem<ProfileEntity>
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Notification;
        [SerializeField]
        private AvatarDrawer Avatar;

        protected ProfileEntity CurrentFriend { get; set; }
        protected ProfileConfigData Config { get; set; }

        protected IFriends Friends { get; set; }

        private void Awake()
        {
            Friends = CBSModule.Get<CBSFriendsModule>();
            Config = CBSScriptable.Get<ProfileConfigData>();
        }

        public void Display(ProfileEntity data)
        {
            CurrentFriend = data;
            DisplayName.text = CurrentFriend.DisplayName;

            var avatarInfo = data.Avatar;
            var onlineInfo = data.OnlineStatus;
            var id = data.ProfileID;
            if (gameObject.activeInHierarchy)
            {
                Avatar.LoadProfileAvatar(id, avatarInfo);
                Avatar.DrawOnlineStatus(onlineInfo);
                Avatar.SetClickable(data.ProfileID);
            }
            if (Config.EnableOnlineStatus)
            {
                var isOnline = onlineInfo != null && onlineInfo.IsOnline;
                if (isOnline)
                {
                    Notification.text = ProfileTXTHandler.ONLINE_TITLE;
                }
                else if (onlineInfo != null)
                {
                    var lastSeenSpan = onlineInfo.LastSeenOnline();
                    Notification.text = ProfileTXTHandler.GetLastOnlineNotification(lastSeenSpan);
                }
                else
                {
                    Notification.text = string.Empty;
                }
            }
            else
            {
                Notification.text = string.Empty;
            }
        }

        public void ShowInfo()
        {
            string userID = CurrentFriend.ProfileID;
            new PopupViewer().ShowUserInfo(userID);
        }
    }
}
