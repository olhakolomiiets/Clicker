using CBS.Core;
using CBS.Models;
using CBS.Utils;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ClanMemberUI : MonoBehaviour, IScrollableItem<ClanMemberUIRequest>
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text ClanRole;
        [SerializeField]
        private Text Level;
        [SerializeField]
        private GameObject KickBtn;
        [SerializeField]
        private GameObject ChangeRoleBtn;
        [SerializeField]
        private AvatarDrawer Avatar;

        private readonly string LevelTitle = "Level ";

        private IProfile Profile { get; set; }
        private string ProfileID { get; set; }
        private string RoleID { get; set; }
        private List<ClanRoleInfo> Roles { get; set; }
        private ClanMember ClanMember { get; set; }

        private void Awake()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
        }

        private void OnDisable()
        {
            ProfileID = string.Empty;
        }

        public void Display(ClanMemberUIRequest request)
        {
            Roles = request.AvailableRoles;
            ClanMember = request.ClanMember;
            var profileEntity = ClanMember.ProfileEntity;
            var levelInfo = profileEntity.Level ?? new EntityLevelInfo();
            var avatarInfo = profileEntity.Avatar ?? new AvatarInfo();
            var onlineInfo = profileEntity.OnlineStatus;
            ProfileID = ClanMember.ProfileID;
            RoleID = ClanMember.RoleID;
            var isMine = ProfileID == Profile.ProfileID;
            KickBtn.SetActive(!isMine);
            ChangeRoleBtn.SetActive(!isMine);

            // draw role
            ClanRole.text = ClanMember.RoleName;
            DisplayName.text = profileEntity.DisplayName;
            Level.text = LevelTitle + levelInfo.Level.GetValueOrDefault().ToString();

            // draw avatar
            Avatar.LoadProfileAvatar(ProfileID, avatarInfo);
            Avatar.SetClickable(ProfileID);
            Avatar.DrawOnlineStatus(onlineInfo);
        }

        // button events
        public void KickHandler()
        {
            new PopupViewer().ShowYesNoPopup(new YesOrNoPopupRequest
            {
                Title = ClanTXTHandler.WarningTitle,
                Body = ClanTXTHandler.RemoveMemberWarning,
                OnYesAction = ProcessKickMember
            });
        }

        public void ChangeRoleHandler()
        {
            new PopupViewer().ShowChangeRolePopup(new ChangeRolePopupRequest
            {
                CurrentRoleID = RoleID,
                Roles = Roles,
                SelectAction = ProcessChangeRole
            });
        }

        private void ProcessKickMember()
        {
            CBSModule.Get<CBSClanModule>().KickClanMember(ProfileID, onKick =>
            {
                if (onKick.IsSuccess)
                {
                    gameObject.SetActive(false);
                }
                else
                {
                    new PopupViewer().ShowFabError(onKick.Error);
                }
            });
        }

        private void ProcessChangeRole(string roleID)
        {
            CBSModule.Get<CBSClanModule>().ChangeMemberRole(ProfileID, roleID, onChange =>
            {
                if (onChange.IsSuccess)
                {
                    var newRole = onChange.NewRole;
                    ClanRole.text = newRole.DisplayName;
                    ClanMember.RoleID = newRole.RoleID;
                    ClanMember.RoleName = newRole.DisplayName;
                }
                else
                {
                    new PopupViewer().ShowFabError(onChange.Error);
                }
            });
        }
    }
}
