using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class StoreWindow : MonoBehaviour
    {
        [SerializeField]
        private StoreTitleScroller TitleScroller;
        [SerializeField]
        private ToggleGroup TitleGroup;
        [SerializeField]
        private StoreContentScroller ContentScroller;
        [SerializeField]
        private StoreLoad LoadType;

        private IStore Store { get; set; }
        private StorePrefabs StorePrefabs { get; set; }

        private void Awake()
        {
            Store = CBSModule.Get<CBSStoreModule>();
            StorePrefabs = CBSScriptable.Get<StorePrefabs>();
        }

        private void OnEnable()
        {
            ClearStore();
            LoadStore();
        }

        private void LoadStore()
        {
            if (LoadType == StoreLoad.LOAD_ALL_AT_ONCE)
            {
                Store.GetStores(OnGetStores);
            }
            else
            {
                Store.GetStoreTitles(OnGetStoreTitles);
            }
        }

        private void ClearStore()
        {
            TitleScroller.HideAll();
            ContentScroller.HideAll();
        }

        private void SpawnStoresTitle(List<CBSStoreTitle> stores)
        {
            var titlePrefab = StorePrefabs.StoreTitle;
            var titlesObject = TitleScroller.Spawn(titlePrefab, stores);
            var titlesUIs = titlesObject.Select(x => x.GetComponent<StoreTitle>());
            foreach (var titleUI in titlesUIs)
            {
                titleUI.SetChangeAction(OnSelectTitle);
                titleUI.SetGroup(TitleGroup);
            }
            titlesUIs.FirstOrDefault()?.Activate();
        }

        private void SpawnStoreContent(List<CBSStoreItem> items)
        {
            var contentPrefab = StorePrefabs.StoreItem;
            ContentScroller.Spawn(contentPrefab, items);
        }

        // events
        private void OnGetStores(CBSGetStoresResult result)
        {
            if (result.IsSuccess)
            {
                var stores = result.Stores;
                if (stores != null && stores.Count > 0)
                {
                    SpawnStoresTitle(stores.Select(x => x as CBSStoreTitle).ToList());
                }
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnGetStore(CBSGetStoreResult result)
        {
            if (result.IsSuccess)
            {
                var store = result.Store;
                var items = store.Items;
                SpawnStoreContent(items);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnGetStoreTitles(CBSGetStoreTitlesResult result)
        {
            if (result.IsSuccess)
            {
                var titles = result.StoreTitles;
                SpawnStoresTitle(titles);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnSelectTitle(CBSStoreTitle storeTitle)
        {
            var storeID = storeTitle.ID;
            if (LoadType == StoreLoad.LOAD_ALL_AT_ONCE)
            {
                var store = storeTitle as CBSStore;
                var items = store.Items;
                SpawnStoreContent(items);
            }
            else
            {
                Store.GetStoreByID(storeID, OnGetStore);
            }
        }

        // button click
        public void CloseStore()
        {
            gameObject.SetActive(false);
        }
    }
}
