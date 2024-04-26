using CBS.Core;
using CBS.Models;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ClanInvationResult : MonoBehaviour, IScrollableItem<ClanInvitationInfo>
    {
        [SerializeField]
        private Text Name;
        [SerializeField]
        private Text Expires;
        [SerializeField]
        private ClanAvatarDrawer Avatar;

        private string ClanID { get; set; }
        public Action AcceptAction { get; set; }

        public void Display(ClanInvitationInfo invitation)
        {
            ClanID = invitation.ClanID;
            var clanEntity = invitation.ClanEntity;
            var avatarInfo = clanEntity.Avatar;
            var displayName = clanEntity.DisplayName;
            Name.text = displayName;
            Expires.text = invitation.Expires.ToLocalTime().ToString("MM/dd/yyyy H:mm");
            Avatar.Load(ClanID, avatarInfo);
        }

        // button events
        public void OnAccept()
        {
            CBSModule.Get<CBSClanModule>().AcceptInvite(ClanID, onAccept =>
            {
                if (onAccept.IsSuccess)
                {
                    gameObject.SetActive(false);
                    new PopupViewer().ShowSimplePopup(new PopupRequest
                    {
                        Title = ClanTXTHandler.SuccessTitle,
                        Body = ClanTXTHandler.JoinClan,
                        OnOkAction = OnJoinClan
                    });
                }
                else
                {
                    new PopupViewer().ShowSimplePopup(new PopupRequest
                    {
                        Title = ClanTXTHandler.ErrorTitle,
                        Body = onAccept.Error.Message
                    });
                }
            });
        }

        public void OnDecline()
        {
            CBSModule.Get<CBSClanModule>().DeclineInvite(ClanID, onDecline =>
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

        // events
        private void OnJoinClan()
        {
            AcceptAction?.Invoke();
        }
    }
}
