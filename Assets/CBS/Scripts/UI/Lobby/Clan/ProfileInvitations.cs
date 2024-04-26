using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class ProfileInvitations : MonoBehaviour, IClanScreen
    {
        [SerializeField]
        private BaseScroller Scroller;

        private IClan CBSClan { get; set; }

        private List<ClanInvitationInfo> Invitations { get; set; }

        private ClanPrefabs Prefabs { get; set; }
        public Action OnBack { get; set; }

        private void Awake()
        {
            Prefabs = CBSScriptable.Get<ClanPrefabs>();
            CBSClan = CBSModule.Get<CBSClanModule>();
            Scroller.OnSpawn += OnClanSpawn;
        }

        private void OnDestroy()
        {
            Scroller.OnSpawn -= OnClanSpawn;
        }

        private void OnEnable()
        {
            Scroller.Clear();
            CBSClan.GetProfileInvitations(CBSClanConstraints.Default(), OnGetInvitations);
        }

        private void OnGetInvitations(CBSGetProfileInvationsResult result)
        {
            if (result.IsSuccess)
            {
                var itemPrefab = Prefabs.ClanInviteResult;
                Invitations = result.Invites;
                Scroller.SpawnItems(itemPrefab, Invitations.Count);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnClanSpawn(GameObject uiItem, int index)
        {
            var clan = Invitations[index];
            uiItem.GetComponent<ClanInvationResult>().Display(clan);
            uiItem.GetComponent<ClanInvationResult>().AcceptAction = BackHandler;
        }

        public void BackHandler()
        {
            OnBack?.Invoke();
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
