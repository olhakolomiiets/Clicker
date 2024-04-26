using CBS.Core;
using CBS.Models;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ClanRequestedUser : MonoBehaviour, IScrollableItem<ClanRequestInfo>
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Expires;
        [SerializeField]
        private AvatarDrawer Avatar;

        private string ProfileID { get; set; }

        private void OnDisable()
        {
            ProfileID = string.Empty;
        }

        public void Display(ClanRequestInfo profile)
        {
            var profileEntity = profile.ProfileEntity;
            ProfileID = profileEntity.ProfileID;

            Expires.text = profile.Expires.ToLocalTime().ToString("MM/dd/yyyy H:mm");
            DisplayName.text = profileEntity.DisplayName;
            // draw avatar
            var avatarInfo = profileEntity.Avatar;
            Avatar.LoadProfileAvatar(ProfileID, avatarInfo);
            Avatar.SetClickable(ProfileID);
            // online status
            var onlineInfo = profileEntity.OnlineStatus;
            Avatar.DrawOnlineStatus(onlineInfo);
        }

        // button events
        public void OnAccept()
        {
            CBSModule.Get<CBSClanModule>().AcceptProfileJoinRequest(ProfileID, onAccept =>
            {
                if (onAccept.IsSuccess)
                {
                    gameObject.SetActive(false);
                }
                else
                {
                    new PopupViewer().ShowFabError(onAccept.Error);
                }
            });
        }

        public void OnDecline()
        {
            CBSModule.Get<CBSClanModule>().DeclineProfileJoinRequest(ProfileID, onDecline =>
            {
                if (onDecline.IsSuccess)
                {
                    gameObject.SetActive(false);
                }
                else
                {
                    new PopupViewer().ShowFabError(onDecline.Error);
                }
            });
        }
    }
}
