using CBS.Core;
using CBS.Models;
using CBS.Scriptable;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class ClanRequests : MonoBehaviour, IClanScreen
    {
        [SerializeField]
        private BaseScroller Scroller;

        private IClan CBSClan { get; set; }

        private List<ClanRequestInfo> RequestList { get; set; }

        private ClanPrefabs Prefabs { get; set; }
        public Action OnBack { get; set; }

        private void Awake()
        {
            Prefabs = CBSScriptable.Get<ClanPrefabs>();
            CBSClan = CBSModule.Get<CBSClanModule>();
            Scroller.OnSpawn += OnSpawnItem;
        }

        private void OnDestroy()
        {
            Scroller.OnSpawn -= OnSpawnItem;
        }

        private void OnEnable()
        {
            Scroller.Clear();
            CBSClan.GetClanJoinRequestsList(CBSProfileConstraints.Default(), OnGetRequests);
        }

        private void OnGetRequests(CBSGetClanJoinRequestListResult result)
        {
            if (result.IsSuccess)
            {
                var itemPrefab = Prefabs.ClanRequestedUser;
                RequestList = result.RequestList;
                Scroller.SpawnItems(itemPrefab, RequestList.Count);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnSpawnItem(GameObject uiItem, int index)
        {
            var profile = RequestList[index];
            uiItem.GetComponent<ClanRequestedUser>().Display(profile);
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
