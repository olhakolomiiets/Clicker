using CBS.Models;
using System.Linq;

namespace CBS.UI
{
    public class OnlineFriendsView : FriendsView
    {
        public override FriendsTabTitle TabTitle => FriendsTabTitle.ONLINE;

        protected override void OnFriendsGet(CBSGetFriendsResult result)
        {
            if (result.IsSuccess)
            {
                var uiPrefab = Prefabs.FriendUI;
                var list = result.Friends.Where(x => x.IsOnline()).ToList();
                Scroller.Spawn(uiPrefab, list);
            }
        }
    }
}