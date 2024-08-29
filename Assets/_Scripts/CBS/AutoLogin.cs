using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using UnityEngine;

namespace CBS.UI
{
    public class AutoLogin : MonoBehaviour
    {
        public static AutoLogin instance = null;
        public event Action<CBSLoginResult> OnLogined;
        private IAuth Auth { get; set; }

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
            DontDestroyOnLoad(gameObject);
        }
        private void Start()
        {
            Auth = CBSModule.Get<CBSAuthModule>();

            OnLoginWithdeviceID();
        }

        public void OnLoginWithdeviceID()
        {
            new PopupViewer().ShowLoadingPopup();
            Auth.LoginWithDevice(OnUserLogined);
        }

        private void OnUserLogined(CBSLoginResult result)
        {
            new PopupViewer().HideLoadingPopup();
            if (result.IsSuccess)
            {
                OnLogined?.Invoke(result);
            }
            else
            {
                // show error message
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
