using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "EventsIcons", menuName = "CBS/Add new EventsIcons Sprite pack")]
    public class EventsIcons : IconsData
    {
        public List<IconData> BackgroundSprites;

        public override string ResourcePath => "EventsIcons";

        public override string EditorPath => "Assets/CBS_External/Resources";

        public override string EditorAssetName => "EventsIcons.asset";

        public virtual Sprite GetBackgroundSprite(string id)
        {
            if (AllSprites == null) return null;
            if (string.IsNullOrEmpty(id)) return null;
            var existedData = BackgroundSprites.FirstOrDefault(x => x.ID == id);
            return existedData == null ? null : existedData.Sprite;
        }

#if UNITY_EDITOR
        public void SaveBackgroundSprite(string id, Sprite sprite)
        {
            if (AllSprites == null)
                AllSprites = new List<IconData>();
            if (string.IsNullOrEmpty(id)) return;

            var existedData = BackgroundSprites.FirstOrDefault(x => x.ID == id);

            if (existedData != null)
            {
                existedData.Sprite = sprite;
            }
            else
            {
                BackgroundSprites.Add(new IconData
                {
                    ID = id,
                    Sprite = sprite
                });
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void RemoveBackgroundSprite(string id)
        {
            if (AllSprites == null)
                return;
            if (string.IsNullOrEmpty(id)) return;
            var existedData = BackgroundSprites.FirstOrDefault(x => x.ID == id);

            if (existedData == null)
                return;

            BackgroundSprites.Remove(existedData);
            BackgroundSprites.TrimExcess();

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
