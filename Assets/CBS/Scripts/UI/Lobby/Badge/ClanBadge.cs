using CBS.Models;
using UnityEngine;

namespace CBS.UI
{
    public class ClanBadge : BaseBadge
    {
        [SerializeField]
        private ClanBadgeMode DisplayMode;

        private IClan CBSClan { get; set; }
        private IProfile CBSProfile { get; set; }

        private void Awake()
        {
            CBSProfile = CBSModule.Get<CBSProfileModule>();
            CBSClan = CBSModule.Get<CBSClanModule>();

            Back.SetActive(false);

            // add listeners
            CBSClan.OnCreateClan += OnCreateClan;
            CBSClan.OnLeaveClan += OnLeaveClan;
            CBSClan.OnJoinClan += OnProfileJoinClan;
            CBSClan.OnDisbandClan += OnKickMember;
            CBSClan.OnProfileAccepted += OnAcceptRequest;
            CBSClan.OnProfileDeclined += OnDeclineRequest;
            CBSClan.OnDeclineInvation += OnDeclineInvation;
            CBSClan.OnProfileAcceptInvation += OnAcceptInvation;
        }

        private void OnDestroy()
        {
            // remove listeners
            CBSClan.OnCreateClan -= OnCreateClan;
            CBSClan.OnLeaveClan -= OnLeaveClan;
            CBSClan.OnJoinClan -= OnProfileJoinClan;
            CBSClan.OnDisbandClan -= OnKickMember;
            CBSClan.OnProfileAccepted -= OnAcceptRequest;
            CBSClan.OnProfileDeclined -= OnDeclineRequest;
            CBSClan.OnDeclineInvation -= OnDeclineInvation;
            CBSClan.OnProfileAcceptInvation -= OnAcceptInvation;
        }

        private void OnEnable()
        {
            UpdateCount(0);
            CheckState();
        }

        private void CheckState()
        {
            CBSClan.GetClanBadge(OnGetBadge);
        }

        private void OnGetBadge(CBSGetClanBadgeResult result)
        {
            if (result.IsSuccess)
            {
                var count = 0;
                if (DisplayMode == ClanBadgeMode.ALL)
                    count = result.InvationsCount + result.RequestsCount;
                else if (DisplayMode == ClanBadgeMode.INVATIONS)
                    count = result.InvationsCount;
                else if (DisplayMode == ClanBadgeMode.REQUESTS)
                    count = result.RequestsCount;
                UpdateCount(count);
            }
        }

        // events
        private void OnProfileJoinClan(CBSJoinToClanResult result)
        {
            CheckState();
        }

        private void OnLeaveClan()
        {
            CheckState();
        }

        private void OnCreateClan(CBSCreateClanResult result)
        {
            CheckState();
        }

        private void OnKickMember()
        {
            CheckState();
        }

        private void OnAcceptRequest(CBSAcceptDeclineClanRequestResult result)
        {
            CheckState();
        }

        private void OnDeclineRequest(CBSAcceptDeclineClanRequestResult result)
        {
            CheckState();
        }

        private void OnDeclineInvation(CBSDeclineInviteResult result)
        {
            CheckState();
        }

        private void OnAcceptInvation(CBSJoinToClanResult result)
        {
            CheckState();
        }
    }
}
