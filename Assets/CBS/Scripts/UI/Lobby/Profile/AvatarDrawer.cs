using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace CBS.UI
{
    public class AvatarDrawer : MonoBehaviour
    {
        [SerializeField]
        protected private Image AvatarImage;
        [SerializeField]
        protected private Image OnlineImage;

        protected ProfileConfigData Config { get; set; }

        private AvatarIcons AvatarsData { get; set; }

        protected AvatarDisplayOptions DisplayOption { get; set; }

        protected bool UseCache { get; set; }
        protected bool UseOnline { get; set; }
        protected string ProfileID { get; set; }

        private void Awake()
        {
            Config = CBSScriptable.Get<ProfileConfigData>();
            AvatarsData = CBSScriptable.Get<AvatarIcons>();
            UseCache = Config.UseCacheForAvatars;
            DisplayOption = Config.AvatarDisplay;
            UseOnline = Config.EnableOnlineStatus;
            OnlineImage?.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            DisplayDefaultAvatar();
        }

        private void OnDisable()
        {
            ProfileID = string.Empty;
            StopAllCoroutines();
        }

        public void DrawOnlineStatus(OnlineStatusData info)
        {
            if (!UseOnline || OnlineImage == null)
                return;
            OnlineImage.gameObject.SetActive(true);
            var isOnline = info != null && info.IsOnline;
            OnlineImage.color = isOnline ? Color.green : Color.red;
        }

        public void LoadAvatarFromUrl(string url, string profileID)
        {
            if (DisplayOption == AvatarDisplayOptions.ONLY_DEFAULT || DisplayOption == AvatarDisplayOptions.LOAD_AVATAR_SPRITE)
                return;

            StartCoroutine(DisplayFromUrl(url, profileID));
        }

        public void LoadProfileAvatar(string profileID, AvatarInfo info)
        {
            if (DisplayOption == AvatarDisplayOptions.ONLY_DEFAULT || info == null)
            {
                DisplayDefaultAvatar();
            }
            else if (DisplayOption == AvatarDisplayOptions.LOAD_AVATAR_URL)
            {
                if (UseCache && CacheUtils.IsInCache(profileID))
                {
                    var tex = CacheUtils.GetTexture(profileID);
                    var avatarSprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
                    AvatarImage.sprite = avatarSprite;
                }

                var imageUrl = info.AvatarURL;
                if (gameObject.activeInHierarchy)
                    StartCoroutine(DisplayFromUrl(imageUrl, profileID));
            }
            else if (DisplayOption == AvatarDisplayOptions.LOAD_AVATAR_SPRITE)
            {
                var avatarID = info.AvatarID;
                DisplaySpriteAvatar(avatarID);
            }
        }

        public void LoadProfileAvatar(string profileID)
        {
            if (DisplayOption == AvatarDisplayOptions.ONLY_DEFAULT)
            {
                DisplayDefaultAvatar();
            }
            else if (DisplayOption == AvatarDisplayOptions.LOAD_AVATAR_URL)
            {
                if (UseCache && CacheUtils.IsInCache(profileID))
                {
                    var tex = CacheUtils.GetTexture(profileID);
                    var avatarSprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
                    AvatarImage.sprite = avatarSprite;
                }

                CBSModule.Get<CBSProfileModule>().GetProfileDetail(new CBSGetProfileRequest
                {
                    ProfileID = profileID
                }, onGet =>
                {
                    if (onGet.IsSuccess)
                    {
                        var imageUrl = onGet.Avatar.AvatarURL;
                        if (gameObject.activeInHierarchy)
                            StartCoroutine(DisplayFromUrl(imageUrl, profileID));
                    }
                    else
                    {
                        DisplayDefaultAvatar();
                    }
                });
            }
            else if (DisplayOption == AvatarDisplayOptions.LOAD_AVATAR_SPRITE)
            {
                CBSModule.Get<CBSProfileModule>().GetProfileAvatarID(profileID, onGet =>
                {
                    if (onGet.IsSuccess)
                    {
                        var avatarID = onGet.AvatarID;
                        DisplaySpriteAvatar(avatarID);
                    }
                    else
                    {
                        DisplayDefaultAvatar();
                    }
                });
            }
        }

        public void SetClickable(string profileID)
        {
            ProfileID = profileID;
        }

        public virtual void ClickAvatar()
        {
            if (!string.IsNullOrEmpty(ProfileID))
            {
                new PopupViewer().ShowUserInfo(ProfileID);
            }
        }

        public virtual void DisplayDefaultAvatar()
        {
            AvatarImage.sprite = Config.DefaultAvatar;
        }

        public virtual void DisplaySpriteAvatar(string avatarID)
        {
            var spriteAvatar = AvatarsData.GetSprite(avatarID);
            if (spriteAvatar == null)
            {
                DisplayDefaultAvatar();
            }
            else
            {
                AvatarImage.sprite = spriteAvatar;
            }
        }

        protected IEnumerator DisplayFromUrl(string url, string profile)
        {
            if (string.IsNullOrEmpty(url))
                yield break;

            if (UseCache && CacheUtils.IsInCache(url))
            {
                var tex = CacheUtils.GetTexture(url);
                var avatarSprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
                AvatarImage.sprite = avatarSprite;
                yield break;
            }

            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
            {
                yield return www.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (www.result != UnityWebRequest.Result.Success)
#else
                if (www.isNetworkError)
#endif
                {
                    DisplayDefaultAvatar();
                }
                else
                {
                    var tex = DownloadHandlerTexture.GetContent(www);

                    if (UseCache)
                    {
                        var bytes = tex.EncodeToPNG();
                        CacheUtils.Save(url, bytes);
                        CacheUtils.Save(profile, bytes);
                    }

                    if (tex == null)
                    {
                        DisplayDefaultAvatar();
                    }
                    else
                    {
                        var avatarSprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
                        AvatarImage.sprite = avatarSprite;
                    }
                }
            }
        }
    }
}