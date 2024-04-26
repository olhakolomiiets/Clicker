using CBS.Models;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ClanInfoForm : MonoBehaviour
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Members;
        [SerializeField]
        private Text Level;
        [SerializeField]
        private Text Description;
        [SerializeField]
        private GameObject RequestButton;
        [SerializeField]
        private GameObject JoinButton;
        [SerializeField]
        private ClanAvatarDrawer Avatar;

        private IClan Clan { get; set; }
        private IProfile Profile { get; set; }
        private ClanEntity ClanEntity { get; set; }
        public Action OnJoin { get; set; }

        private void Awake()
        {
            Clan = CBSModule.Get<CBSClanModule>();
            Profile = CBSModule.Get<CBSProfileModule>();
        }

        public void Display(ClanEntity clanEntity)
        {
            ClanEntity = clanEntity;
            var avatarInfo = ClanEntity.Avatar;
            var isMine = ClanEntity.ClanID == Profile.ClanID;

            DisplayName.text = clanEntity.DisplayName;
            Description.text = clanEntity.Description;
            Members.text = clanEntity.MembersCount.ToString();
            Level.text = clanEntity.LevelInfo.Level.GetValueOrDefault().ToString();

            RequestButton.SetActive(clanEntity.Visibility == ClanVisibility.BY_REQUEST && !isMine);
            JoinButton.SetActive(clanEntity.Visibility == ClanVisibility.OPEN && !isMine);

            Avatar.Load(clanEntity.ClanID, avatarInfo);
        }

        public void SendJoinRequest()
        {
            Clan.SendJoinRequest(ClanEntity.ClanID, onJoin =>
            {

                RequestButton.SetActive(!onJoin.IsSuccess);

                if (onJoin.IsSuccess)
                {
                    new PopupViewer().ShowSimplePopup(new PopupRequest
                    {
                        Title = ClanTXTHandler.SuccessTitle,
                        Body = ClanTXTHandler.ClanSendJoin
                    });
                }
                else
                {
                    new PopupViewer().ShowSimplePopup(new PopupRequest
                    {
                        Title = ClanTXTHandler.ErrorTitle,
                        Body = onJoin.Error.Message
                    });
                }
            });
        }

        public void JoinHandler()
        {
            Clan.JoinToClan(ClanEntity.ClanID, onJoin =>
            {

                JoinButton.SetActive(!onJoin.IsSuccess);

                if (onJoin.IsSuccess)
                {
                    new PopupViewer().ShowSimplePopup(new PopupRequest
                    {
                        Title = ClanTXTHandler.SuccessTitle,
                        Body = ClanTXTHandler.JoinClan
                    });
                    OnJoin?.Invoke();
                }
                else
                {
                    new PopupViewer().ShowSimplePopup(new PopupRequest
                    {
                        Title = ClanTXTHandler.ErrorTitle,
                        Body = onJoin.Error.Message
                    });
                }
            });
        }
    }
}
