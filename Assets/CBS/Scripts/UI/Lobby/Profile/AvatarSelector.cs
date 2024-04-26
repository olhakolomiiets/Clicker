using CBS.Models;
using CBS.Scriptable;
using UnityEngine;

namespace CBS.UI
{
    public class AvatarSelector : MonoBehaviour
    {
        [SerializeField]
        private AvatarListScroller Scroller;

        private IProfile Profile { get; set; }
        private ProfilePrefabs Prefabs { get; set; }

        private void Awake()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
            Prefabs = CBSScriptable.Get<ProfilePrefabs>();
        }

        private void OnEnable()
        {
            LoadAvatars();
        }

        private void LoadAvatars()
        {
            Profile.GetProfileAvatarTableWithStates(OnGetAvatars);
        }

        private void DisplayAvatars(AvatarsTableWithStates table)
        {
            Scroller.HideAll();
            var states = table.AvatarStates;
            if (states != null)
            {
                var prefabUI = Prefabs.AvatarState;
                var list = Scroller.Spawn(prefabUI, states);
                foreach (var ui in list)
                    ui.GetComponent<AvatarStateDrawer>().SetCallbacks(SelectRequest, BuyRequest);
            }
        }

        // events
        private void OnGetAvatars(CBSGetProfileAvatarTableWithStatesResult result)
        {
            if (result.IsSuccess)
            {
                var states = result.TableWithStates;
                DisplayAvatars(states);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void SelectRequest(CBSAvatarState avatar)
        {
            var avatarID = avatar.ID;
            Profile.UpdateAvatarID(avatarID, onSelect =>
            {
                if (onSelect.IsSuccess)
                {
                    var updatedStates = onSelect.UpdatedStates;
                    DisplayAvatars(updatedStates);
                }
                else
                {
                    new PopupViewer().ShowFabError(onSelect.Error);
                }
            });
        }

        private void BuyRequest(CBSAvatarState avatar)
        {
            var avatarID = avatar.ID;
            Profile.PurchaseAvatar(avatarID, onPurchase =>
            {
                if (onPurchase.IsSuccess)
                {
                    var updatedStates = onPurchase.UpdatedStates;
                    DisplayAvatars(updatedStates);
                }
                else
                {
                    new PopupViewer().ShowFabError(onPurchase.Error);
                }
            });
        }
    }
}
