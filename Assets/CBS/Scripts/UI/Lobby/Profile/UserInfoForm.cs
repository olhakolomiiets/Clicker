using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class UserInfoForm : MonoBehaviour
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Notification;
        [SerializeField]
        private Text Level;
        [SerializeField]
        private Text Exp;
        [SerializeField]
        private Text Clan;
        [SerializeField]
        private GameObject AddToFriendBtn;
        [SerializeField]
        private GameObject RemoveFriendBtn;
        [SerializeField]
        private GameObject DeclineFriendBtn;
        [SerializeField]
        private GameObject InviteToClanBtn;
        [SerializeField]
        private GameObject ViewClanBtn;
        [SerializeField]
        private AvatarDrawer AvatarDrawer;
        [SerializeField]
        private SharedFriendsLoader FriendsLoader;

        private CBSGetProfileResult CurrentInfo { get; set; }

        private string ProfileID { get; set; }
        private IFriends CBSFriends { get; set; }
        private IClan CBSClan { get; set; }
        private IProfile CBSProfile { get; set; }
        private ProfileConfigData Config { get; set; }

        private void Awake()
        {
            CBSFriends = CBSModule.Get<CBSFriendsModule>();
            CBSClan = CBSModule.Get<CBSClanModule>();
            CBSProfile = CBSModule.Get<CBSProfileModule>();
            Config = CBSScriptable.Get<ProfileConfigData>();
        }

        public void Display(CBSGetProfileResult info)
        {
            CurrentInfo = info;
            DisplayName.text = CurrentInfo.DisplayName;
            ProfileID = CurrentInfo.ProfileID;
            Level.text = CurrentInfo.Level.Level.ToString() + " Level";
            Exp.text = CurrentInfo.Level.Expirience.ToString();

            var clanID = CurrentInfo.ClanID;
            var clanExists = !string.IsNullOrEmpty(clanID);
            var clanName = clanExists ? CurrentInfo.ClanEntity.DisplayName : string.Empty;
            ViewClanBtn.SetActive(clanExists);
            Clan.text = clanExists == true ? clanName : ClanTXTHandler.ClanNotExistTitle;
            // resize clan text
            var clanRect = Clan.GetComponent<RectTransform>();
            clanRect.sizeDelta = new Vector2(Clan.preferredWidth, clanRect.sizeDelta.y);
            // load avatar image
            AvatarDrawer.LoadProfileAvatar(ProfileID, info.Avatar);
            // draw online
            var onlineInfo = info.OnlineStatus;
            AvatarDrawer.DrawOnlineStatus(onlineInfo);
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
            // load shared friends
            FriendsLoader.LoadFriends(ProfileID);

            CheckFriendsExist();
            CheckClanInvite();
        }

        private void CheckFriendsExist()
        {
            AddToFriendBtn.SetActive(false);
            RemoveFriendBtn.SetActive(false);
            DeclineFriendBtn.SetActive(false);

            CBSFriends.CheckFriendship(ProfileID, onCheck =>
            {
                if (onCheck.IsSuccess)
                {
                    AddToFriendBtn.SetActive(!onCheck.ExistAsAcceptedFriend && !onCheck.ExistAsRequestedFriend && !onCheck.WaitForProfileAccept);
                    RemoveFriendBtn.SetActive(onCheck.ExistAsAcceptedFriend);
                    DeclineFriendBtn.SetActive(onCheck.ExistAsRequestedFriend);
                }
            });
        }

        private void CheckClanInvite()
        {
            var viewerHasClan = CBSProfile.ExistInClan;
            InviteToClanBtn.SetActive(viewerHasClan);
        }

        // buttons events
        public void SendFriendsRequest()
        {
            CBSFriends.SendFriendsRequest(ProfileID, onSend =>
            {
                if (onSend.IsSuccess)
                {
                    CheckFriendsExist();
                }
            });
        }

        public void RemoveFriend()
        {
            CBSFriends.RemoveFriend(ProfileID, onDecline =>
            {
                if (onDecline.IsSuccess)
                {
                    CheckFriendsExist();
                }
            });
        }

        public void DeclineFriendRequest()
        {
            CBSFriends.DeclineFriendRequest(ProfileID, onDecline =>
            {
                if (onDecline.IsSuccess)
                {
                    CheckFriendsExist();
                }
            });
        }

        public void SendDirectMessage()
        {
            var cbsChat = CBSModule.Get<CBSChatModule>();
            var chat = cbsChat.GetOrCreatePrivateChatWithProfile(ProfileID);
            ChatUtils.ShowSimpleChat(chat);
        }

        public void InviteToClan()
        {
            CBSClan.InviteToClan(ProfileID, onInvite =>
            {
                if (onInvite.IsSuccess)
                {
                    new PopupViewer().ShowSimplePopup(new PopupRequest
                    {
                        Title = ClanTXTHandler.SuccessTitle,
                        Body = ClanTXTHandler.ClanSendInvite
                    });
                }
                else
                {
                    new PopupViewer().ShowFabError(onInvite.Error);
                }
            });
        }

        public void ShowClanInfo()
        {
            var clanID = CurrentInfo.ClanID;
            new PopupViewer().ShowClanInfo(clanID);
        }
    }
}
