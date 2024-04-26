using CBS.Models;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class UpdateAvatarUrlForm : MonoBehaviour
    {
        [SerializeField]
        private InputField URLInput;

        private IProfile Profile { get; set; }

        private void Start()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
        }

        private void CleanInput()
        {
            URLInput.text = string.Empty;
        }

        // buttons click
        public void UploadUrl()
        {
            var url = URLInput.text;
            if (string.IsNullOrEmpty(url))
                return;
            Profile.UpdateAvatarUrl(url, OnUpdateAvatar);
        }

        // events
        private void OnUpdateAvatar(CBSUpdateAvatarUrlResult result)
        {
            if (!result.IsSuccess)
            {
                new PopupViewer().ShowFabError(result.Error);
            }
            else
            {
                CleanInput();
            }
        }
    }
}

