using UnityEngine;

namespace CBS.UI
{
    public class BaseFriendsTab : BaseTab<string>
    {
        [SerializeField]
        private FriendsTabTitle TabTitle;

        private void Start()
        {
            TabObject = TabTitle.ToString();
        }

        public FriendsTabTitle GetTitle()
        {
            return TabTitle;
        }
    }
}
