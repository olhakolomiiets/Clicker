using System;
using System.Collections.Generic;

namespace CBS
{
    [Serializable]
    public class LevelRewardObject
    {
        public List<LevelRewardItem> BundledItems;

        public List<LevelRewardItem> Lootboxes;

        public List<LevelRewardCurrency> BundledVirtualCurrencies;

        public bool AddExpirience;
        public bool ExpirienceOnce;
        public int ExpirienceValue;

        public bool IsEmpty()
        {
            return (BundledItems == null || BundledItems != null && BundledItems.Count == 0)
                && (Lootboxes == null || Lootboxes != null && Lootboxes.Count == 0)
                && (BundledVirtualCurrencies == null || BundledVirtualCurrencies != null && BundledVirtualCurrencies.Count == 0)
                && (AddExpirience == false || (AddExpirience && ExpirienceValue <= 0));
        }

        public int GetPositionCount()
        {
            var bundleCount = BundledItems == null ? 0 : BundledItems.Count;
            var lootCount = Lootboxes == null ? 0 : Lootboxes.Count;
            var currencyCount = BundledVirtualCurrencies == null ? 0 : BundledVirtualCurrencies.Count;
            return bundleCount + lootCount + currencyCount;
        }
    }
}