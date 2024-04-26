using CBS.Models;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI.Matchmaking
{
    public class MatchmakingPreviewPlayer : MonoBehaviour
    {
        [SerializeField]
        private AvatarDrawer Avatar;
        [SerializeField]
        private Text DisplayName;

        private IProfile Profile { get; set; }

        private void Awake()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
        }

        public void DrawUser(CBSMatchmakingPlayer player)
        {
            Profile.GetProfileDetail(new CBSGetProfileRequest
            {
                ProfileID = player.ProfileID,
                Constraints = new CBSProfileConstraints
                {
                    LoadAvatar = true
                }
            },
            OnGetProfileInfo);
        }

        private void OnGetProfileInfo(CBSGetProfileResult result)
        {
            if (result.IsSuccess)
            {
                var avatarInfo = result.Avatar;
                var profileID = result.ProfileID;
                Avatar.LoadProfileAvatar(profileID, avatarInfo);
                DisplayName.text = result.DisplayName;
            }
        }
    }
}
