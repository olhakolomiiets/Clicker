using System;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using CBS.Models;
using CBS.Utils;
using UnityEngine.UI;

namespace CBS.UI
{

    public class UserManager : MonoBehaviour
    {

        [SerializeField] private Text NicknameLabel;
        [SerializeField] private GameObject UserNamePanel;
        [SerializeField] private GameObject EditNamePanel;
        [SerializeField] private InputField EditInput;

        GeneralGameData _currentGameData;

        private IProfile CBSProfile { get; set; }
        private IAuth Auth { get; set; }

        private void Awake()
        {
            CBSProfile = CBSModule.Get<CBSProfileModule>();
            Auth = CBSModule.Get<CBSAuthModule>();
        }

        private void OnEnable()
        {
            DisplayUI();
            CBSProfile.OnDisplayNameUpdated += OnUserNameUpdated;
            CBSProfile.GetAccountInfo(OnAccountInfoGetted);
        }

        private void OnDisable()
        {
            CBSProfile.OnDisplayNameUpdated -= OnUserNameUpdated;
        }

        public void PrepareGameData(GeneralGameData gameData)
        {
            _currentGameData = gameData;
        }


        public void UpdateNickname()
        {
            string newName = EditInput.text;

            if (string.IsNullOrEmpty(newName))
            {
                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = AuthTXTHandler.ErrorTitle,
                    Body = AuthTXTHandler.InvalidInput
                });
                return;
            }
            CBSProfile.UpdateDisplayName(newName, onComplete =>
            {
                if (!onComplete.IsSuccess)
                {
                    new PopupViewer().ShowFabError(onComplete.Error);
                }
            });
        }

        private void OnUserNameUpdated(CBSUpdateDisplayNameResult result)
        {
            if (result.IsSuccess)
            {
                NicknameLabel.text = result.DisplayName;
                ShowNickname();

                _currentGameData.UserName = NicknameLabel.text;
            }
        }

        public void ShowNickname()
        {
            UserNamePanel.SetActive(true);
            EditNamePanel.SetActive(false);
        }

        public void ShowEditName()
        {
            EditInput.text = CBSProfile.DisplayName;
            UserNamePanel.SetActive(false);
            EditNamePanel.SetActive(true);
        }

        private void OnAccountInfoGetted(CBSGetAccountInfoResult result)
        {
            DisplayUI();
        }

        private void DisplayUI()
        {
            ShowNickname();
            NicknameLabel.text = CBSProfile.DisplayName;
            EditInput.text = string.Empty;
        }
    }
}