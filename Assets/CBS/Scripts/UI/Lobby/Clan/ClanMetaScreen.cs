using CBS.Models;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ClanMetaScreen : MonoBehaviour, IClanScreen
    {
        [SerializeField]
        private GameObject InfoPanel;
        [SerializeField]
        private GameObject EditAvatarPanel;
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Members;
        [SerializeField]
        private Text Description;
        [SerializeField]
        private Text Level;
        [SerializeField]
        private Text Expirience;
        [SerializeField]
        private Slider ExpSlider;
        [SerializeField]
        private ClanAvatarDrawer Avatar;
        [SerializeField]
        private Toggle IsOpen;
        [SerializeField]
        private ClanAvatarSelector AvatarSelector;

        private IClan Clan { get; set; }
        private IProfile Profile { get; set; }
        private ClanFullInfo ClanInfo { get; set; }
        public Action OnBack { get; set; }

        private void Awake()
        {
            Clan = CBSModule.Get<CBSClanModule>();
            Profile = CBSModule.Get<CBSProfileModule>();
            IsOpen.onValueChanged.AddListener(OnVisibilityChange);
        }

        private void OnDestroy()
        {
            IsOpen.onValueChanged.RemoveListener(OnVisibilityChange);
        }

        private void OnGetClanFullInformation(CBSGetClanFullInfoResult result)
        {
            if (result.IsSuccess)
            {
                InfoPanel.SetActive(true);
                var info = result.Info;
                Display(info);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void Display(ClanFullInfo clanInfo)
        {
            ClanInfo = clanInfo;
            var avatarInfo = ClanInfo.AvatarInfo;
            var levelInfo = clanInfo.LevelInfo ?? new LevelInfo();

            DisplayName.text = clanInfo.DisplayName;
            Description.text = clanInfo.Description;
            Members.text = clanInfo.MembersCount.ToString();
            IsOpen.isOn = clanInfo.Visibility == ClanVisibility.OPEN;

            DisplayLevel(levelInfo);
            Avatar.Load(clanInfo.ClanID, avatarInfo);
        }

        private void DisplayLevel(LevelInfo level)
        {
            Level.text = level.CurrentLevel.ToString();

            int curExp = level.CurrentExp;
            int nextExp = level.NextLevelExp;
            int prevExp = level.PrevLevelExp;
            float expVal = (float)(curExp - prevExp) / (float)(nextExp - prevExp);
            Expirience.text = curExp.ToString() + "/" + nextExp.ToString();
            ExpSlider.value = expVal;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            InfoPanel.SetActive(false);
            EditAvatarPanel.SetActive(false);
            Clan.GetClanFullInformation(Profile.ClanID, OnGetClanFullInformation);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void BackHandler()
        {
            OnBack?.Invoke();
        }

        public void EditDisplayNameHandler()
        {
            var initialName = ClanInfo.DisplayName;
            new PopupViewer().ShowEditTextPopup(new EditTextPopupRequest
            {
                Title = ClanTXTHandler.EditNameTitle,
                InitialInput = initialName,
                SaveAction = (newName) =>
                {
                    Clan.UpdateClanDisplayName(newName, onUpdate =>
                    {
                        if (onUpdate.IsSuccess)
                        {
                            ClanInfo.DisplayName = newName;
                            DisplayName.text = newName;
                        }
                        else
                        {
                            new PopupViewer().ShowFabError(onUpdate.Error);
                            DisplayName.text = initialName;
                        }
                    });
                }
            });
        }

        public void EditDescriptionHandler()
        {
            var initialDescription = ClanInfo.Description;
            new PopupViewer().ShowEditTextPopup(new EditTextPopupRequest
            {
                Title = ClanTXTHandler.EditDescriptionTitle,
                InitialInput = initialDescription,
                SaveAction = (newDescription) =>
                {
                    Clan.UpdateClanDescription(newDescription, onUpdate =>
                    {
                        if (onUpdate.IsSuccess)
                        {
                            ClanInfo.Description = newDescription;
                            Description.text = newDescription;
                        }
                        else
                        {
                            new PopupViewer().ShowFabError(onUpdate.Error);
                            Description.text = initialDescription;
                        }
                    });
                }
            });
        }

        public void EditAvatarHandler()
        {
            InfoPanel.SetActive(false);
            EditAvatarPanel.SetActive(true);

            var avatarInfo = ClanInfo.AvatarInfo;
            AvatarSelector.Load(avatarInfo);
        }

        public void SaveAvatarHandler()
        {
            InfoPanel.SetActive(true);
            EditAvatarPanel.SetActive(false);

            var newAvatar = AvatarSelector.GetAvatarInfo();
            Clan.UpdateClanAvatar(newAvatar, onUpdate =>
            {
                if (onUpdate.IsSuccess)
                {
                    Avatar.Load(ClanInfo.ClanID, newAvatar);
                    ClanInfo.AvatarInfo = newAvatar;
                }
                else
                {
                    new PopupViewer().ShowFabError(onUpdate.Error);
                }
            });
        }

        public void CancelAvatarHandler()
        {
            InfoPanel.SetActive(true);
            EditAvatarPanel.SetActive(false);
        }

        public void OnVisibilityChange(bool val)
        {
            var newVisibility = val ? ClanVisibility.OPEN : ClanVisibility.BY_REQUEST;
            Clan.UpdateClanVisibility(newVisibility, onUpdate =>
            {
                if (onUpdate.IsSuccess)
                {
                    ClanInfo.Visibility = newVisibility;
                }
                else
                {
                    IsOpen.SetIsOnWithoutNotify(!val);
                    new PopupViewer().ShowFabError(onUpdate.Error);
                }
            });
        }
    }
}
