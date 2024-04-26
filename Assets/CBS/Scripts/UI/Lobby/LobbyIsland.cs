using CBS.Scriptable;
using UnityEngine;

namespace CBS.UI
{
    public class LobbyIsland : MonoBehaviour
    {
        [SerializeField]
        private WindowID Window;

        public void ClickHandler()
        {
            GameObject windowPrefab = null;
            switch (Window)
            {
                case WindowID.CLAN:
                    {
                        windowPrefab = CBSScriptable.Get<ClanPrefabs>().WindowLoader;
                        break;
                    }
                case WindowID.EVENTS:
                    {
                        windowPrefab = CBSScriptable.Get<EventsPrefabs>().EventWindow;
                        break;
                    }
                case WindowID.FORGE:
                    {
                        windowPrefab = CBSScriptable.Get<CraftPrefabs>().CraftWindow;
                        break;
                    }
                case WindowID.FRIENDS:
                    {
                        windowPrefab = CBSScriptable.Get<FriendsPrefabs>().FriendsWindow;
                        break;
                    }
                case WindowID.MATCHMAKING:
                    {
                        windowPrefab = CBSScriptable.Get<MatchmakingPrefabs>().MatchmakingWindow;
                        break;
                    }
                case WindowID.SHOP:
                    {
                        windowPrefab = CBSScriptable.Get<StorePrefabs>().StoreWindows;
                        break;
                    }
                default:
                    {
                        return;
                    }
            }
            UIView.ShowWindow(windowPrefab);
        }
    }
}
