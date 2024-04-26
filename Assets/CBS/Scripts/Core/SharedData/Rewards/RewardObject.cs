using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    [Serializable]
    public class RewardObject
    {
        public List<string> BundledItems;

        public List<string> Lootboxes;

        public Dictionary<string, uint> BundledVirtualCurrencies;

        public bool AddExpirience;

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

        public RewardObject MergeReward(RewardObject rewardToMerge)
        {
            if (rewardToMerge == null)
                return this;
            // merge items
            var mergedItems = new List<string>();
            if (BundledItems == null)
                mergedItems = rewardToMerge.BundledItems;
            else if (BundledItems != null && rewardToMerge.BundledItems != null)
            {
                mergedItems = BundledItems.Concat(rewardToMerge.BundledItems).ToList();
            }
            // merge lootboxes
            var mergedLootBoxes = new List<string>();
            if (Lootboxes == null)
                mergedLootBoxes = rewardToMerge.Lootboxes;
            else if (Lootboxes != null && rewardToMerge.Lootboxes != null)
            {
                mergedLootBoxes = Lootboxes.Concat(rewardToMerge.Lootboxes).ToList();
            }
            // merge currency
            var mergedCurrency = new Dictionary<string, uint>();
            if (BundledVirtualCurrencies == null)
            {
                mergedCurrency = rewardToMerge.BundledVirtualCurrencies;
            }
            else if (BundledVirtualCurrencies != null && rewardToMerge.BundledVirtualCurrencies != null)
            {
                mergedCurrency = BundledVirtualCurrencies.Concat(rewardToMerge.BundledVirtualCurrencies).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => (uint)x.Sum(y => y.Value));
            }

            // merge exp
            var mergedExp = 0;
            var hasExp = AddExpirience == true || rewardToMerge.AddExpirience == true;
            if (hasExp)
            {
                if (AddExpirience) mergedExp += ExpirienceValue;
                if (rewardToMerge.AddExpirience) mergedExp += rewardToMerge.ExpirienceValue;
            }

            return new RewardObject
            {
                BundledItems = mergedItems,
                Lootboxes = mergedLootBoxes,
                BundledVirtualCurrencies = mergedCurrency,
                AddExpirience = hasExp,
                ExpirienceValue = mergedExp
            };
        }
    }
}