using CBS.Utils;
using System.Collections.Generic;

namespace CBS.Models
{
    public class StoresContainer
    {
        public const string GLOBAL_OFFER_STORE_ID = "CBSStoreGlobalOffer";
        public const string PROFILE_OFFER_STORE_ID = "CBSStoreProfileOffer";

        public List<string> StoresIDs;

        public Dictionary<string, StoreLimitationMeta> LimitationMeta;

        public List<string> GetStoreIDs()
        {
            return StoresIDs ?? new List<string>();
        }

        public void AddStoreID(string id)
        {
            StoresIDs = StoresIDs ?? new List<string>();
            if (StoresIDs.Contains(id))
            {
                return;
            }
            StoresIDs.Add(id);
        }

        public void RemoveID(string id)
        {
            if (StoresIDs == null)
                return;
            if (StoresIDs.Contains(id))
            {
                StoresIDs.Remove(id);
            }
        }

        public void CheckForRemoveLimitation(string storeID, string itemID)
        {
            if (LimitationMeta == null)
                return;
            var limitationID = StoreUtils.GetStoreItemID(storeID, itemID);
            if (LimitationMeta.ContainsKey(limitationID))
                LimitationMeta.Remove(limitationID);
        }

        public void AddOrUpdateLimitation(string storeID, string itemID, StoreLimitationMeta meta)
        {
            if (LimitationMeta == null)
                LimitationMeta = new Dictionary<string, StoreLimitationMeta>();
            var limitationID = StoreUtils.GetStoreItemID(storeID, itemID);
            LimitationMeta[limitationID] = meta;
        }

        public bool ContainLimitationMeta(string storeID, string itemID)
        {
            if (LimitationMeta == null)
                return false;
            var limitationID = StoreUtils.GetStoreItemID(storeID, itemID);
            return LimitationMeta.ContainsKey(limitationID);
        }

        public StoreLimitationMeta GetLimitationMeta(string storeID, string itemID)
        {
            if (LimitationMeta == null)
                return null;
            var limitationID = StoreUtils.GetStoreItemID(storeID, itemID);
            return LimitationMeta[limitationID];
        }
    }
}
