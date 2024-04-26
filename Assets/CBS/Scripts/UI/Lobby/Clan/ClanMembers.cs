using CBS.Models;
using CBS.Scriptable;
using System;
using System.Linq;
using UnityEngine;

namespace CBS.UI
{
    public class ClanMembers : MonoBehaviour, IClanScreen
    {
        [SerializeField]
        private ClanMembersScroller Scroller;

        private IProfile Profile { get; set; }
        private IClan CBSClan { get; set; }
        private ClanPrefabs Prefabs { get; set; }
        public Action OnBack { get; set; }

        public string ClanID => Profile.ClanID;

        private void Awake()
        {
            CBSClan = CBSModule.Get<CBSClanModule>();
            Profile = CBSModule.Get<CBSProfileModule>();
            Prefabs = CBSScriptable.Get<ClanPrefabs>();
        }

        private void OnGetMembers(CBSGetClanMembersResult result)
        {
            if (result.IsSuccess)
            {
                var profilePrefab = Prefabs.ClanMember;
                var profiles = result.Members;
                var roles = result.AvailableRoles;
                Scroller.Spawn(profilePrefab, profiles.Select(x => new ClanMemberUIRequest
                {
                    ClanMember = x,
                    AvailableRoles = roles
                }).ToList());
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Scroller.HideAll();
            CBSClan.GetClanMemberships(ClanID, CBSProfileConstraints.Default(), OnGetMembers);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void BackHandler()
        {
            OnBack?.Invoke();
        }
    }
}
