using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class CreateClanForm : MonoBehaviour, IClanScreen
    {
        [SerializeField]
        private InputField NameInput;
        [SerializeField]
        private InputField DescriptionInput;
        [SerializeField]
        private ClanAvatarSelector AvatarSelector;
        [SerializeField]
        private Toggle VisibilityToggle;
        [SerializeField]
        private Button CreateButton;

        private IClan CBSClan { get; set; }
        private ClanPrefabs Prefabs { get; set; }
        public Action OnBack { get; set; }

        private void Awake()
        {
            CBSClan = CBSModule.Get<CBSClanModule>();
            Prefabs = CBSScriptable.Get<ClanPrefabs>();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            CreateButton.interactable = true;
            AvatarSelector.Load(new ClanAvatarInfo());
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private bool ValidInputs()
        {
            bool validName = !string.IsNullOrEmpty(NameInput.text);
            bool validDescription = !string.IsNullOrEmpty(DescriptionInput.text);

            bool fieldsValid = validName & validDescription;
            if (!fieldsValid)
            {
                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = ClanTXTHandler.ErrorTitle,
                    Body = ClanTXTHandler.InvalidInput
                });
                return false;
            }
            return true;
        }

        // button click
        public void CreateClan()
        {
            if (!ValidInputs())
                return;
            var clanName = NameInput.text;
            var clanDescription = DescriptionInput.text;
            var avatarInfo = AvatarSelector.GetAvatarInfo();
            var visibility = VisibilityToggle.isOn ? ClanVisibility.OPEN : ClanVisibility.BY_REQUEST;

            var createResult = new CBSCreateClanRequest
            {
                DisplayName = clanName,
                Description = clanDescription,
                Visibility = visibility,
                AvatarInfo = avatarInfo
            };

            CreateButton.interactable = false;

            CBSClan.CreateClan(createResult, onCreate =>
            {
                CreateButton.interactable = true;
                if (onCreate.IsSuccess)
                {
                    string clanID = onCreate.ClanID;
                    new PopupViewer().ShowSimplePopup(new PopupRequest
                    {
                        Title = ClanTXTHandler.SuccessTitle,
                        Body = ClanTXTHandler.ClanCreated,
                        OnOkAction = () =>
                        {
                            OnClanCreated(clanID);
                        }
                    });
                }
                else
                {
                    new PopupViewer().ShowSimplePopup(new PopupRequest
                    {
                        Title = ClanTXTHandler.ErrorTitle,
                        Body = onCreate.Error.Message
                    });
                }
            });
        }

        private void OnClanCreated(string clanID)
        {
            OnBack?.Invoke();
        }

        public void ReturnBack()
        {
            OnBack?.Invoke();
        }
    }
}
