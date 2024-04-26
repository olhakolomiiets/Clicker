using CBS.Scriptable;
using CBS.Utils;
using UnityEngine;

namespace CBS.UI
{
    public class AvatarWindow : MonoBehaviour
    {
        [SerializeField]
        private GameObject UrlUploadForm;
        [SerializeField]
        private GameObject AvatarSelectForm;

        private ProfileConfigData ProfileConfig { get; set; }
        private AvatarDisplayOptions DisplayOption { get; set; }

        private void Awake()
        {
            ProfileConfig = CBSScriptable.Get<ProfileConfigData>();
            DisplayOption = ProfileConfig.AvatarDisplay;
        }

        private void OnEnable()
        {
            ShowAvatarForm();
        }

        private void ShowAvatarForm()
        {
            UrlUploadForm.SetActive(DisplayOption == AvatarDisplayOptions.LOAD_AVATAR_URL);
            AvatarSelectForm.SetActive(DisplayOption == AvatarDisplayOptions.LOAD_AVATAR_SPRITE);
        }
    }
}
