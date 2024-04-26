using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class SimpleAvatarSelector : MonoBehaviour
    {
        [SerializeField]
        private Image AvatarHolder;

        private Dictionary<string, IconData> Icons { get; set; }
        private int SelectIndex { get; set; }
        private ClanAvatarInfo AvatarInfo { get; set; }

        public void Load(List<IconData> icons, ClanAvatarInfo avatarInfo)
        {
            AvatarInfo = avatarInfo;
            var avatarID = AvatarInfo.SimpleIconID;
            Icons = icons.ToDictionary(x => x.ID, x => x);
            if (string.IsNullOrEmpty(avatarID))
            {
                SelectIndex = 0;
            }
            else
            {
                if (Icons.ContainsKey(avatarID))
                {
                    SelectIndex = icons.IndexOf(Icons[avatarID]);
                }
                else
                {
                    SelectIndex = 0;
                }
            }
            if (Icons.Count > 0)
            {
                DisplayFromIndex(SelectIndex);
            }
        }

        private void DisplayFromIndex(int index)
        {
            var iconPair = Icons.ElementAt(index);
            var sprite = iconPair.Value.Sprite;
            AvatarHolder.sprite = sprite;
        }

        public void SelectRightHandler()
        {
            if (Icons.Count == 0 || SelectIndex >= Icons.Count - 1)
                return;
            SelectIndex++;
            DisplayFromIndex(SelectIndex);
        }

        public void SelectLeftHandler()
        {
            if (Icons.Count == 0 || SelectIndex <= 0)
                return;
            SelectIndex--;
            DisplayFromIndex(SelectIndex);
        }

        public ClanAvatarInfo GetAvatarInfo()
        {
            if (Icons.Count == 0)
            {
                AvatarInfo.SimpleIconID = string.Empty;
            }
            else
            {
                var iconPair = Icons.ElementAt(SelectIndex);
                AvatarInfo.SimpleIconID = iconPair.Key;
            }
            return AvatarInfo;
        }
    }
}
