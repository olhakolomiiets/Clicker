using CBS.Models;
using CBS.Scriptable;
using UnityEngine;

namespace CBS.UI
{
    public class ClanAvatarSelector : MonoBehaviour
    {
        [SerializeField]
        private SimpleAvatarSelector SimpleSelector;
        [SerializeField]
        private ComplexAvatarSelector ComplexSelector;

        public void Load(ClanAvatarInfo avatarInfo)
        {
            var iconsData = CBSScriptable.Get<ClanIcons>();
            var viewType = iconsData.DisplayMode;
            SimpleSelector.gameObject.SetActive(viewType == ClanAvatarViewMode.SIMPLE);
            ComplexSelector.gameObject.SetActive(viewType == ClanAvatarViewMode.COMPLEX);
            if (viewType == ClanAvatarViewMode.SIMPLE)
                SimpleSelector.Load(iconsData.AllSprites, avatarInfo);
            else
                ComplexSelector.Load(iconsData.Backgrounds, iconsData.Foregrounds, iconsData.Emblems, avatarInfo);
        }

        public ClanAvatarInfo GetAvatarInfo()
        {
            var iconsData = CBSScriptable.Get<ClanIcons>();
            var viewType = iconsData.DisplayMode;
            return viewType == ClanAvatarViewMode.SIMPLE ? SimpleSelector.GetAvatarInfo() : ComplexSelector.GetAvatarInfo();
        }
    }
}
