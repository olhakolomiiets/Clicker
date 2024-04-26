

using CBS.Core;
using System;
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSStoreItemMeta : ICustomData<CBSStoreItemCustomData>
    {
        public bool Enable;
        public string SlotDisplayName;
        public string Description;
        public bool HasQuantityLimit;
        public DatePeriod QuanityLimitPeriod;
        public int QuantityLimit;
        public Dictionary<string, int> Discounts;
        public string InstanceID;
        public bool HasDuration;
        public int OfferDuration;
        public DateTime? EndDate;

        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }
        public bool CompressCustomData => false;

        public T GetCustomData<T>() where T : CBSStoreItemCustomData
        {
            return JsonPlugin.FromJson<T>(CustomRawData);
        }
    }
}
