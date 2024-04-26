using CBS.Models;
using CBS.Scriptable;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ClanAvatarDrawer : MonoBehaviour
    {
        [SerializeField]
        private Image AvatarImage;
        [SerializeField]
        private Image BackgroundImage;
        [SerializeField]
        private Image ForegroundImage;
        [SerializeField]
        private Image EmblemImage;

        private ClanAvatarInfo ClanAvatar { get; set; }
        private string ClanID { get; set; }
        private ClanIcons ClanIcons { get; set; }
        private ClanAvatarViewMode DisplayMode { get; set; }

        private void Awake()
        {
            ClanIcons = CBSScriptable.Get<ClanIcons>();
            DisplayMode = ClanIcons.DisplayMode;

            AvatarImage.gameObject.SetActive(DisplayMode == ClanAvatarViewMode.SIMPLE);
            BackgroundImage.gameObject.SetActive(DisplayMode == ClanAvatarViewMode.COMPLEX);
            ForegroundImage.gameObject.SetActive(DisplayMode == ClanAvatarViewMode.COMPLEX);
            EmblemImage.gameObject.SetActive(DisplayMode == ClanAvatarViewMode.COMPLEX);
        }

        public void ClickAvatar()
        {
            if (!string.IsNullOrEmpty(ClanID))
            {
                new PopupViewer().ShowClanInfo(ClanID);
            }
        }

        public void DisplayDefaultAvatar()
        {
            AvatarImage.sprite = ClanIcons.DefaultAvatar;
        }

        public void Load(string clanID, ClanAvatarInfo clanAvatar)
        {
            ClanID = clanID;
            ClanAvatar = clanAvatar;
            if (DisplayMode == ClanAvatarViewMode.SIMPLE)
                DisplaySimpleIcon(clanAvatar);
            else
                DisplayComplexIcon(clanAvatar);
        }

        private void DisplaySimpleIcon(ClanAvatarInfo clanAvatar)
        {
            var avatarID = clanAvatar.SimpleIconID;
            if (string.IsNullOrEmpty(avatarID))
                DisplayDefaultAvatar();
            else
                AvatarImage.sprite = ClanIcons.GetSimpeIcon(avatarID);
        }

        private void DisplayComplexIcon(ClanAvatarInfo clanAvatar)
        {
            clanAvatar = clanAvatar ?? new ClanAvatarInfo();
            var backgroundInfo = clanAvatar.ComplexBackground ?? new ColorAvatar();
            var foregroundInfo = clanAvatar.ComplexForeground ?? new ColorAvatar();
            var emblemInfo = clanAvatar.ComplexEmblem ?? new ColorAvatar();

            BackgroundImage.sprite = ClanIcons.GetBackgroundIcon(backgroundInfo.AvatarID);
            BackgroundImage.color = ClanIcons.GetColorByIndex(backgroundInfo.ColorID);

            ForegroundImage.sprite = ClanIcons.GetForegroundIcon(foregroundInfo.AvatarID);
            ForegroundImage.color = ClanIcons.GetColorByIndex(foregroundInfo.ColorID);

            EmblemImage.sprite = ClanIcons.GetEmblemIcon(emblemInfo.AvatarID);
            EmblemImage.color = ClanIcons.GetColorByIndex(emblemInfo.ColorID);
        }
    }
}
