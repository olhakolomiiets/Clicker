using CBS.Core;
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSStoreItem : ICustomData<CBSStoreItemCustomData>
    {
        public string ItemID;
        public string StoreID;
        public string DisplayName;
        public string Description;
        public bool HasQuantityLimit;
        public StoreLimitationInfo Limitation;
        public Dictionary<string, int> Discounts;
        public Dictionary<string, uint> Prices;

        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }
        public bool CompressCustomData => false;

        public T GetCustomData<T>() where T : CBSStoreItemCustomData
        {
            return JsonPlugin.FromJson<T>(CustomRawData);
        }

        public bool HasDiscount(string currencyCode)
        {
            if (Discounts == null || Discounts.Count == 0)
                return false;
            return Discounts.ContainsKey(currencyCode);
        }

        public int GetDiscount(string currencyCode)
        {
            if (!HasDiscount(currencyCode))
                return 0;
            return Discounts[currencyCode];
        }
    }
}
