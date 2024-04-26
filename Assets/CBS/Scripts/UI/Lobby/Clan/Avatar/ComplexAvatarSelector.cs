using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ComplexAvatarSelector : MonoBehaviour
    {
        [SerializeField]
        private Image BackgroundHolder;
        [SerializeField]
        private Image ForegroundHolder;
        [SerializeField]
        private Image EmblemHolder;
        [SerializeField]
        private ComplexAvatarTabListener TabListener;
        [SerializeField]
        private Transform ColorButtonsRoot;

        private ClanPrefabs Prefabs { get; set; }
        private ClanIcons Icons { get; set; }
        private ClanAvatarInfo AvatarInfo { get; set; }
        private Dictionary<AvatarComplexPart, List<IconData>> IconsPreset { get; set; }
        private Dictionary<AvatarComplexPart, int> IconsIndex { get; set; }
        private Dictionary<AvatarComplexPart, int> ColorsIndex { get; set; }

        private void Awake()
        {
            Prefabs = CBSScriptable.Get<ClanPrefabs>();
            Icons = CBSScriptable.Get<ClanIcons>();
            DrawColorTable();
            TabListener.OnTabSelected += OnChangeComplexPart;
        }

        private void OnDestroy()
        {
            TabListener.OnTabSelected -= OnChangeComplexPart;
        }

        public void Load(List<IconData> backgrounds, List<IconData> foregrounds, List<IconData> emblems, ClanAvatarInfo avatarInfo)
        {
            AvatarInfo = avatarInfo ?? new ClanAvatarInfo();
            IconsPreset = new Dictionary<AvatarComplexPart, List<IconData>>();
            IconsPreset[AvatarComplexPart.BACKGROUND] = backgrounds;
            IconsPreset[AvatarComplexPart.FOREGROUND] = foregrounds;
            IconsPreset[AvatarComplexPart.EMBLEM] = emblems;

            ColorsIndex = new Dictionary<AvatarComplexPart, int>();
            IconsIndex = new Dictionary<AvatarComplexPart, int>();
            // check background
            var avatarBackgroundColor = AvatarInfo.ComplexBackground ?? new ColorAvatar();
            if (string.IsNullOrEmpty(avatarBackgroundColor.AvatarID))
            {
                IconsIndex[AvatarComplexPart.BACKGROUND] = 0;
            }
            else
            {
                if (IconsPreset[AvatarComplexPart.BACKGROUND].Any(x => x.ID == avatarBackgroundColor.AvatarID))
                    IconsIndex[AvatarComplexPart.BACKGROUND] = IconsPreset[AvatarComplexPart.BACKGROUND].IndexOf(IconsPreset[AvatarComplexPart.BACKGROUND].FirstOrDefault(x => x.ID == avatarBackgroundColor.AvatarID));
                else
                    IconsIndex[AvatarComplexPart.BACKGROUND] = 0;
            }
            ColorsIndex[AvatarComplexPart.BACKGROUND] = avatarBackgroundColor.ColorID;
            // check foreground
            var avatarForegroundColor = AvatarInfo.ComplexForeground ?? new ColorAvatar();
            if (string.IsNullOrEmpty(avatarForegroundColor.AvatarID))
                IconsIndex[AvatarComplexPart.FOREGROUND] = 0;
            else
            {
                if (IconsPreset[AvatarComplexPart.FOREGROUND].Any(x => x.ID == avatarForegroundColor.AvatarID))
                    IconsIndex[AvatarComplexPart.FOREGROUND] = IconsPreset[AvatarComplexPart.FOREGROUND].IndexOf(IconsPreset[AvatarComplexPart.FOREGROUND].FirstOrDefault(x => x.ID == avatarForegroundColor.AvatarID));
                else
                    IconsIndex[AvatarComplexPart.FOREGROUND] = 0;
            }
            ColorsIndex[AvatarComplexPart.FOREGROUND] = avatarForegroundColor.ColorID;
            // check emblem
            var avatarEmblemColor = AvatarInfo.ComplexEmblem ?? new ColorAvatar();
            if (string.IsNullOrEmpty(avatarEmblemColor.AvatarID))
                IconsIndex[AvatarComplexPart.EMBLEM] = 0;
            else
            {
                if (IconsPreset[AvatarComplexPart.EMBLEM].Any(x => x.ID == avatarEmblemColor.AvatarID))
                    IconsIndex[AvatarComplexPart.EMBLEM] = IconsPreset[AvatarComplexPart.EMBLEM].IndexOf(IconsPreset[AvatarComplexPart.EMBLEM].FirstOrDefault(x => x.ID == avatarEmblemColor.AvatarID));
                else
                    IconsIndex[AvatarComplexPart.EMBLEM] = 0;
            }
            ColorsIndex[AvatarComplexPart.EMBLEM] = avatarEmblemColor.ColorID;

            AvatarInfo.ComplexBackground = avatarBackgroundColor;
            AvatarInfo.ComplexForeground = avatarForegroundColor;
            AvatarInfo.ComplexEmblem = avatarEmblemColor;

            if (IconsPreset[TabListener.ActiveTab].Count > 0)
            {
                DisplayFromPart(TabListener.ActiveTab);
            }
            DrawAvatarColor(AvatarComplexPart.BACKGROUND);
            DrawAvatarColor(AvatarComplexPart.FOREGROUND);
            DrawAvatarColor(AvatarComplexPart.EMBLEM);
        }

        private void DisplayFromPart(AvatarComplexPart part)
        {
            var currentIndex = IconsIndex[part];
            var iconPair = IconsPreset[part].ElementAt(currentIndex);
            var sprite = iconPair.Sprite;
            DrawAvatarSprite(part, sprite);
            DrawAvatarColor(part);
        }

        public void SelectRightHandler()
        {
            if (IconsPreset[TabListener.ActiveTab].Count == 0 || IconsIndex[TabListener.ActiveTab] >= IconsPreset[TabListener.ActiveTab].Count - 1)
                return;
            IconsIndex[TabListener.ActiveTab]++;
            DisplayFromPart(TabListener.ActiveTab);
        }

        public void SelectLeftHandler()
        {
            if (IconsPreset[TabListener.ActiveTab].Count == 0 || IconsIndex[TabListener.ActiveTab] <= 0)
                return;
            IconsIndex[TabListener.ActiveTab]--;
            DisplayFromPart(TabListener.ActiveTab);
        }

        public ClanAvatarInfo GetAvatarInfo()
        {
            AvatarInfo.ComplexBackground.AvatarID = IconsPreset[AvatarComplexPart.BACKGROUND][IconsIndex[AvatarComplexPart.BACKGROUND]].ID;
            AvatarInfo.ComplexForeground.AvatarID = IconsPreset[AvatarComplexPart.FOREGROUND][IconsIndex[AvatarComplexPart.FOREGROUND]].ID;
            AvatarInfo.ComplexEmblem.AvatarID = IconsPreset[AvatarComplexPart.EMBLEM][IconsIndex[AvatarComplexPart.EMBLEM]].ID;

            AvatarInfo.ComplexBackground.ColorID = ColorsIndex[AvatarComplexPart.BACKGROUND];
            AvatarInfo.ComplexForeground.ColorID = ColorsIndex[AvatarComplexPart.FOREGROUND];
            AvatarInfo.ComplexEmblem.ColorID = ColorsIndex[AvatarComplexPart.EMBLEM];

            return AvatarInfo;
        }

        private void OnChangeComplexPart(AvatarComplexPart part)
        {
            if (IconsPreset[TabListener.ActiveTab].Count > 0)
            {
                DisplayFromPart(TabListener.ActiveTab);
            }
        }

        private void DrawAvatarSprite(AvatarComplexPart part, Sprite sprite)
        {
            if (part == AvatarComplexPart.BACKGROUND)
            {
                BackgroundHolder.sprite = sprite;
            }
            else if (part == AvatarComplexPart.FOREGROUND)
            {
                ForegroundHolder.sprite = sprite;
            }
            else if (part == AvatarComplexPart.EMBLEM)
            {
                EmblemHolder.sprite = sprite;
            }
        }

        private void DrawAvatarColor(AvatarComplexPart part)
        {
            var colorIndex = ColorsIndex[part];
            var colors = Icons.ColorsToSet;
            if (colorIndex >= colors.Count)
                colorIndex = colors.Count - 1;
            var color = colors.Count == 0 ? Color.white : colors[colorIndex];
            if (part == AvatarComplexPart.BACKGROUND)
            {
                BackgroundHolder.color = color;
            }
            else if (part == AvatarComplexPart.FOREGROUND)
            {
                ForegroundHolder.color = color;
            }
            else if (part == AvatarComplexPart.EMBLEM)
            {
                EmblemHolder.color = color;
            }
        }

        private void DrawColorTable()
        {
            var prefab = Prefabs.ColorButton;
            var colorSet = Icons.ColorsToSet;
            for (int i = 0; i < colorSet.Count; i++)
            {
                var colorIndex = i;
                var buttonObject = Instantiate(prefab, ColorButtonsRoot);
                buttonObject.GetComponent<Image>().color = colorSet[i];
                buttonObject.GetComponent<Button>().onClick.AddListener(() =>
                {
                    ColorsIndex[TabListener.ActiveTab] = colorIndex;
                    DrawAvatarColor(TabListener.ActiveTab);
                });
            }
        }
    }
}
