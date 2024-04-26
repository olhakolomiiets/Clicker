using CBS.Core;
using CBS.Models;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class LeaderboardUser : MonoBehaviour, IScrollableItem<ProfileLeaderboardEntry>
    {
        [SerializeField]
        private Image Background;
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private PlaceDrawer Place;
        [SerializeField]
        private AvatarDrawer Avatar;
        [SerializeField]
        private Text Value;
        [Header("Colors")]
        [SerializeField]
        private Color DefaultColor;
        [SerializeField]
        private Color ActiveColor;

        public void Display(ProfileLeaderboardEntry data)
        {
            DisplayName.text = data.DisplayName;
            Place.Draw(data.StatisticPosition);
            Value.text = data.StatisticValue.ToString();
            var cbsProfile = CBSModule.Get<CBSProfileModule>();
            string profileID = cbsProfile.ProfileID;
            bool isMine = data.ProfileID == profileID;
            DisplayName.fontStyle = isMine ? FontStyle.Bold : FontStyle.Normal;
            Value.fontStyle = isMine ? FontStyle.Bold : FontStyle.Normal;
            Background.color = isMine ? ActiveColor : DefaultColor;

            var avatarInfo = data.Avatar;
            var onlineInfo = data.OnlineStatus;

            Avatar.LoadProfileAvatar(data.ProfileID, avatarInfo);
            Avatar.DrawOnlineStatus(onlineInfo);
            Avatar.SetClickable(data.ProfileID);
        }
    }
}
