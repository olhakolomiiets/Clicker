using CBS.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ClanSearchForm : MonoBehaviour, IClanScreen
    {
        [SerializeField]
        private InputField SearchInput;
        [SerializeField]
        private ClanInfoForm ClanInfo;

        private IClan CBSClan { get; set; }
        public Action OnBack { get; set; }

        private void Awake()
        {
            CBSClan = CBSModule.Get<CBSClanModule>();
            ClanInfo.OnJoin += OnJoinClan;
        }

        private void OnDestroy()
        {
            ClanInfo.OnJoin -= OnJoinClan;
        }

        private void OnDisable()
        {
            SearchInput.text = string.Empty;
        }

        private void OnGetClan(CBSGetClanEntityResult result)
        {
            if (result.IsSuccess)
            {
                var clanEntity = result.ClanEntity;
                ClanInfo.gameObject.SetActive(true);
                ClanInfo.Display(clanEntity);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnJoinClan()
        {
            BackHandler();
        }

        // button events
        public void OnSearch()
        {
            string searchValue = SearchInput.text;
            if (!string.IsNullOrEmpty(searchValue))
            {
                CBSClan.SearchClanByName(searchValue, OnGetClan);
            }
        }

        public void BackHandler()
        {
            OnBack?.Invoke();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            ClanInfo.gameObject.SetActive(false);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
