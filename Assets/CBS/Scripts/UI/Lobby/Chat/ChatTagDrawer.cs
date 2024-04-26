using CBS.Models;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ChatTagDrawer : MonoBehaviour
    {
        [SerializeField]
        private Text DisplayNameLabel;

        private readonly float WidthOfset = 30f;

        private LayoutElement DrawerLayout { get; set; }

        public string TagProfileID { get; private set; }

        private void Awake()
        {
            DrawerLayout = GetComponent<LayoutElement>();
        }

        public void DrawTag(ChatMember chatMember)
        {
            TagProfileID = chatMember.ProfileID;
            DisplayNameLabel.text = chatMember.DisplayName;
            var preferredWith = DisplayNameLabel.preferredWidth + WidthOfset;
            DrawerLayout.preferredWidth = preferredWith;
        }

        private void OnDisable()
        {
            TagProfileID = null;
        }
    }
}
