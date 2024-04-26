using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "ClanIcons", menuName = "CBS/Add new Tasks Clan Icons")]
    public class ClanIcons : IconsData
    {
        public override string ResourcePath => "ClanIcons";

        public override string EditorPath => "Assets/CBS_External/Resources";

        public override string EditorAssetName => "ClanIcons.asset";

        public Sprite DefaultAvatar;
        public ClanAvatarViewMode DisplayMode;

        public List<IconData> Backgrounds;
        public List<IconData> Foregrounds;
        public List<IconData> Emblems;
        public List<Color> ColorsToSet;

        private Dictionary<string, Sprite> SimpleIconsDictionary { get; set; }
        private Dictionary<string, Sprite> BackgroundIconsDictionary { get; set; }
        private Dictionary<string, Sprite> ForegroundIconsDictionary { get; set; }
        private Dictionary<string, Sprite> EmblemsIconsDictionary { get; set; }

        internal override void Initialize()
        {
            base.Initialize();
            if (AllSprites != null)
                SimpleIconsDictionary = AllSprites.ToDictionary(x => x.ID, x => x.Sprite);
            if (Backgrounds != null)
                BackgroundIconsDictionary = Backgrounds.ToDictionary(x => x.ID, x => x.Sprite);
            if (Foregrounds != null)
                ForegroundIconsDictionary = Foregrounds.ToDictionary(x => x.ID, x => x.Sprite);
            if (Emblems != null)
                EmblemsIconsDictionary = Emblems.ToDictionary(x => x.ID, x => x.Sprite);
        }

        public bool SimpleIconsExist()
        {
            return AllSprites != null && AllSprites.Count > 0;
        }

        public bool ComplexIconsExist()
        {
            return Backgrounds != null && Backgrounds.Count > 0 || Foregrounds != null && Foregrounds.Count > 0 || Emblems != null && Emblems.Count > 0;
        }

        public void RemoveBackground(string id)
        {
            if (Backgrounds == null)
                return;
            if (string.IsNullOrEmpty(id)) return;
            var existedData = Backgrounds.FirstOrDefault(x => x.ID == id);

            if (existedData == null)
                return;

            Backgrounds.Remove(existedData);
            Backgrounds.TrimExcess();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void RemoveForeground(string id)
        {
            if (Foregrounds == null)
                return;
            if (string.IsNullOrEmpty(id)) return;
            var existedData = Foregrounds.FirstOrDefault(x => x.ID == id);

            if (existedData == null)
                return;

            Foregrounds.Remove(existedData);
            Foregrounds.TrimExcess();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void RemoveEmblems(string id)
        {
            if (Emblems == null)
                return;
            if (string.IsNullOrEmpty(id)) return;
            var existedData = Emblems.FirstOrDefault(x => x.ID == id);

            if (existedData == null)
                return;

            Emblems.Remove(existedData);
            Emblems.TrimExcess();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public Sprite GetSimpeIcon(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return SimpleIconsDictionary.ContainsKey(id) ? SimpleIconsDictionary[id] : null;
        }

        public Sprite GetBackgroundIcon(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return BackgroundIconsDictionary.ContainsKey(id) ? BackgroundIconsDictionary[id] : null;
        }

        public Sprite GetForegroundIcon(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return ForegroundIconsDictionary.ContainsKey(id) ? ForegroundIconsDictionary[id] : null;
        }

        public Sprite GetEmblemIcon(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return EmblemsIconsDictionary.ContainsKey(id) ? EmblemsIconsDictionary[id] : null;
        }

        public Color GetColorByIndex(int index)
        {
            if (index >= ColorsToSet.Count - 1)
            {
                return Color.white;
            }
            else
            {
                return ColorsToSet[index];
            }
        }
    }
}
