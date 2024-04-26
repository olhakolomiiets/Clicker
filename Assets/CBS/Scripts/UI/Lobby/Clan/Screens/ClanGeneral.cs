using CBS.Utils;
using System;
using UnityEngine;

namespace CBS.UI
{
    public class ClanGeneral : MonoBehaviour
    {
        [SerializeField]
        private GameObject LeaveIcon;
        [SerializeField]
        private GameObject CreateIcon;
        [SerializeField]
        private GameObject RequestsIcon;
        [SerializeField]
        private GameObject ClanMeta;
        [SerializeField]
        private GameObject Members;
        [SerializeField]
        private GameObject Disband;
        [SerializeField]
        private GameObject Chat;
        [SerializeField]
        private GameObject Inventory;
        [SerializeField]
        private GameObject Tasks;

        public event Action<ClanScreen> OnLoadScreen;
        private IClan Clan { get; set; }
        private IProfile Profile { get; set; }

        private void Awake()
        {
            Clan = CBSModule.Get<CBSClanModule>();
        }

        public void Load(bool existInClan)
        {
            LeaveIcon.SetActive(existInClan);
            Members.SetActive(existInClan);
            RequestsIcon.SetActive(existInClan);
            CreateIcon.SetActive(!existInClan);
            ClanMeta.SetActive(existInClan);
            Disband.SetActive(existInClan);
            Chat.SetActive(existInClan);
            Inventory.SetActive(existInClan);
            Tasks.SetActive(existInClan);

            InitMetaIcon();
        }

        private void InitMetaIcon()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
            if (Profile.ExistInClan)
            {
                ClanMeta.GetComponent<ClanMetaIcon>().Init(Profile.ClanEntity);
            }
        }

        public void ShowCreateClan() => OnLoadScreen?.Invoke(ClanScreen.CREATE_CLAN);

        public void ShowSearchClan() => OnLoadScreen?.Invoke(ClanScreen.SEARCH_CLAN);

        public void ShowInvations() => OnLoadScreen?.Invoke(ClanScreen.INVATIONS);

        public void ShowRequests() => OnLoadScreen?.Invoke(ClanScreen.REQUESTS);

        public void ShowClanMeta() => OnLoadScreen?.Invoke(ClanScreen.CLAN_META);

        public void ShowClanMembers() => OnLoadScreen?.Invoke(ClanScreen.MEMBERS);

        public void ShowChat() => OnLoadScreen?.Invoke(ClanScreen.CHAT);

        public void ShowInventory() => OnLoadScreen?.Invoke(ClanScreen.INVENTORY);

        public void ShowTasks() => OnLoadScreen?.Invoke(ClanScreen.TASKS);

        public void LeaveClanHandler()
        {
            new PopupViewer().ShowYesNoPopup(new YesOrNoPopupRequest
            {
                Title = ClanTXTHandler.WarningTitle,
                Body = ClanTXTHandler.LeaveClanWarning,
                OnYesAction = () =>
                {
                    Clan.LeaveClan(onLeave =>
                    {
                        if (onLeave.IsSuccess)
                        {
                            Load(false);
                        }
                        else
                        {
                            new PopupViewer().ShowFabError(onLeave.Error);
                        }
                    });
                }
            });
        }

        public void DisbandClanHandler()
        {
            new PopupViewer().ShowYesNoPopup(new YesOrNoPopupRequest
            {
                Title = ClanTXTHandler.WarningTitle,
                Body = ClanTXTHandler.DisbandClanWarning,
                OnYesAction = () =>
                {
                    Clan.DisbandClan(onDisband =>
                    {
                        if (onDisband.IsSuccess)
                        {
                            Load(false);
                        }
                        else
                        {
                            new PopupViewer().ShowFabError(onDisband.Error);
                        }
                    });
                }
            });
        }
    }
}
