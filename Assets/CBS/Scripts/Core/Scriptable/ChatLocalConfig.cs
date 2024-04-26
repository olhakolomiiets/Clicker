using System.Collections.Generic;
using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "ChatConfig", menuName = "CBS/Add new Chat Config")]
    public class ChatLocalConfig : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/Core/ChatLocalConfig";

        public bool ShowDate;
        public bool UseUniqueColor;
        public Color DefaultTextColor;
        public Color SystemTextColor;
        public Color BubbleTextColor;
        public Color TagTextColor;
        public Color OwnerBubbleColor;
        public Color SystemBubbleColor;
        public List<Color> ColorTable;
    }
}
