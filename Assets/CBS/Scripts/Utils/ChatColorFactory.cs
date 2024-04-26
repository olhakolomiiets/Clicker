using CBS.Scriptable;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.Utils
{
    public static class ChatColorFactory
    {
        private static Dictionary<string, Color> CacheProfileColor;
        private static ChatLocalConfig ChatData;
        private static List<Color> ColorList;
        private static int ColorIndex = 0;
        private static bool IsInited;

        public static Color GetProfileColor(string profileID)
        {
            if (!IsInited)
                InitFactory();
            if (!ChatData.UseUniqueColor)
            {
                return ChatData.DefaultTextColor;
            }
            if (CacheProfileColor.ContainsKey(profileID))
            {
                return CacheProfileColor[profileID];
            }
            else
            {
                var nextColor = GetNextAvailableColor();
                CacheProfileColor[profileID] = nextColor;
                return nextColor;
            }
        }

        public static Color GetTagColor()
        {
            if (!IsInited)
                InitFactory();
            return ChatData.TagTextColor;
        }

        private static void InitFactory()
        {
            CacheProfileColor = new Dictionary<string, Color>();
            ChatData = CBSScriptable.Get<ChatLocalConfig>();
            ColorList = ChatData.ColorTable;
            ColorList.Shuffle();
            IsInited = true;
        }

        private static Color GetNextAvailableColor()
        {
            if (ColorIndex >= ColorList.Count)
            {
                ColorIndex = 0;
            }
            var nextColor = ColorList[ColorIndex];
            ColorIndex++;
            return nextColor;
        }
    }
}
