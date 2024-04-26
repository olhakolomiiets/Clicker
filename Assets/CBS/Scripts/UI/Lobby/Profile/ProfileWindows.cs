using System;
using UnityEngine;

namespace CBS.UI
{
    public class ProfileWindows : MonoBehaviour
    {
        [SerializeField]
        private ProfileTab[] AllTabes;
        [SerializeField]
        private GameObject Info;
        [SerializeField]
        private GameObject Avatar;

        private ProfileSection Section { get; set; }

        private void Awake()
        {
            foreach (var tab in AllTabes)
                tab.SetSelectAction(OnTabSelected);
        }

        private void OnEnable()
        {
            DisplayView(Section);
        }

        private void DisplayView(ProfileSection title)
        {
            Info.SetActive(title == ProfileSection.INFO);
            Avatar.SetActive(title == ProfileSection.AVATAR);
        }

        // events
        private void OnTabSelected(string title)
        {
            Section = (ProfileSection)Enum.Parse(typeof(ProfileSection), title, true);
            DisplayView(Section);
        }

        // button click
        public void CloseWindow()
        {
            gameObject.SetActive(false);
        }
    }
}
