using CBS.Scriptable;
using System;
using System.Linq;
using UnityEngine;

namespace CBS.UI
{
    public class FriendsWindow : MonoBehaviour
    {
        [SerializeField]
        private BaseFriendsTab[] AllTabes;
        [SerializeField]
        private FriendsSection[] AllView;

        private FriendsTabTitle CurrentTab { get; set; }
        private ProfileConfigData ProfileConfig { get; set; }

        private void Awake()
        {
            ProfileConfig = CBSScriptable.Get<ProfileConfigData>();
            var onlineTab = AllTabes.FirstOrDefault(x => x.GetTitle() == FriendsTabTitle.ONLINE);
            onlineTab?.gameObject.SetActive(ProfileConfig.EnableOnlineStatus);
            foreach (var tab in AllTabes)
                tab.SetSelectAction(OnTabSelected);
        }

        private void OnEnable()
        {
            DisplayView(CurrentTab);
        }

        private void OnTabSelected(string title)
        {
            CurrentTab = (FriendsTabTitle)Enum.Parse(typeof(FriendsTabTitle), title, true);
            DisplayView(CurrentTab);
        }

        private void DisplayView(FriendsTabTitle title)
        {
            foreach (var view in AllView)
            {
                view.Clean();
                view.Hide();
            }
            var activeView = AllView.FirstOrDefault(x => x.TabTitle == title);
            if (activeView != null)
            {
                activeView.SetChatAction(OnChatSelected);
                activeView.gameObject.SetActive(true);
                activeView.Display();
            }
        }

        private void OnChatSelected(string userID)
        {
            foreach (var view in AllView)
            {
                view.Clean();
                view.Hide();
            }
        }
    }

    public enum FriendsTabTitle
    {
        FRIENDS,
        REQUESTED,
        ONLINE
    }
}
