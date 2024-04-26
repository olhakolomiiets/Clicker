using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class SpecialOfferFrameLoader : MonoBehaviour
    {
        [SerializeField]
        private Transform Root;

        private IStore Store { get; set; }
        private StorePrefabs Prefabs { get; set; }

        private Dictionary<int, SpecialOfferFrame> FramePool;
        private Dictionary<string, SpecialOfferFrame> UIPool;

        private void Awake()
        {
            Store = CBSModule.Get<CBSStoreModule>();
            Prefabs = CBSScriptable.Get<StorePrefabs>();
            FramePool = new Dictionary<int, SpecialOfferFrame>();
            UIPool = new Dictionary<string, SpecialOfferFrame>();
        }

        private void OnEnable()
        {
            HideAllFrames();
            LoadAllFrames();
            Store.OnItemPurchased += OnPurchaseItem;
            Store.OnItemPurchasedWithRM += OnPurchaseItemWithRM;
        }

        private void OnDisable()
        {
            Store.OnItemPurchased -= OnPurchaseItem;
            Store.OnItemPurchasedWithRM -= OnPurchaseItemWithRM;
        }

        private void LoadAllFrames()
        {
            Store.GetSpecialOffers(OnGetOffers);
        }

        private void OnGetOffers(CBSGetSpecialOffersResult result)
        {
            if (result.IsSuccess)
            {
                var offers = result.Offers;
                DrawOffers(offers);
            }
        }

        private void DrawOffers(List<CBSSpecialOffer> offers)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                var storeID = offer.StoreID;
                var itemID = offer.ItemID;
                var uniqueID = StoreUtils.GetStoreItemID(storeID, itemID);
                var frame = GetFrameAt(i);
                frame.Load(offer);
                UIPool[uniqueID] = frame;
            }
        }

        private SpecialOfferFrame GetFrameAt(int index)
        {
            if (FramePool.ContainsKey(index))
            {
                var frame = FramePool[index];
                frame.gameObject.SetActive(true);
                return frame;
            }
            else
            {
                var framePrefab = Prefabs.SpecialOfferFrame;
                var frameObject = Instantiate(framePrefab, Root);
                var frameComponent = frameObject.GetComponent<SpecialOfferFrame>();
                FramePool[index] = frameComponent;
                return frameComponent;
            }
        }

        private void HideFrameByID(string uniqueID)
        {
            if (UIPool.ContainsKey(uniqueID))
            {
                UIPool[uniqueID].gameObject.SetActive(false);
            }
        }

        private void HideAllFrames()
        {
            foreach (var framePair in FramePool)
                framePair.Value.gameObject.SetActive(false);
        }

        // events

        private void OnPurchaseItem(CBSPurchaseStoreItemResult result)
        {
            if (result.IsSuccess)
            {
                var storeID = result.StoreID;
                var itemID = result.ItemID;
                var uniqueID = StoreUtils.GetStoreItemID(storeID, itemID);
                HideFrameByID(uniqueID);
            }
        }

        private void OnPurchaseItemWithRM(CBSPurchaseStoreItemWithRMResult result)
        {
            if (result.IsSuccess)
            {
                var storeID = result.StoreID;
                var itemID = result.ItemID;
                var uniqueID = StoreUtils.GetStoreItemID(storeID, itemID);
                HideFrameByID(uniqueID);
            }
        }
    }
}
