using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class RecoveryForm : MonoBehaviour
    {
        [SerializeField]
        private InputField EmailField;

        private IAuth Auth { get; set; }
        private AuthPrefabs AuthUIData { get; set; }

        private void Start()
        {
            Auth = CBSModule.Get<CBSAuthModule>();
            AuthUIData = CBSScriptable.Get<AuthPrefabs>();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void Clear()
        {
            EmailField.text = string.Empty;
        }

        // buttons click
        public void SendRecovery()
        {
            string email = EmailField.text;
            if (string.IsNullOrEmpty(email))
            {
                // show error message
                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = AuthTXTHandler.ErrorTitle,
                    Body = AuthTXTHandler.InvalidInput
                });
            }
            // send request
            Auth.SendPasswordRecovery(email, OnRecoverySent);
        }

        public void ReturnToLogin()
        {
            var loginPrefab = AuthUIData.LoginForm;
            UIView.ShowWindow(loginPrefab);
            gameObject.SetActive(false);
        }

        // events
        private void OnRecoverySent(CBSBaseResult result)
        {
            if (result.IsSuccess)
            {
                string email = EmailField.text;
                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = AuthTXTHandler.SuccessTitle,
                    Body = AuthTXTHandler.GetRecoveryMessage(email),
                    OnOkAction = Clear
                });
            }
            else
            {
                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = AuthTXTHandler.ErrorTitle,
                    Body = result.Error.Message
                });
            }
        }
    }
}
