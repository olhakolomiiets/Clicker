using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class CraftStateContainer
    {
        public Dictionary<string, ItemDependencyState> ItemsState;
        public Dictionary<string, ItemDependencyState> CurrenciesState;

        public bool ReadyToGraft()
        {
            return IsItemsReady() && IsCurrenciesReady() || IsFreeToCraft();
        }

        public bool IsFreeToCraft()
        {
            return (ItemsState == null || ItemsState.Count == 0) && (CurrenciesState == null || CurrenciesState.Count == 0);
        }

        public List<ItemDependencyState> GetDependencyList()
        {
            var items = ItemsState == null ? new List<ItemDependencyState>() : ItemsState.Select(x => x.Value).ToList();
            var currencies = CurrenciesState == null ? new List<ItemDependencyState>() : CurrenciesState.Select(x => x.Value).ToList();
            return items.Concat(currencies).ToList();
        }

        private bool IsItemsReady()
        {
            if (ItemsState == null || ItemsState.Count == 0)
                return true;
            return !ItemsState.Select(x => x.Value).Any(x => !x.IsValid());
        }

        private bool IsCurrenciesReady()
        {
            if (CurrenciesState == null || CurrenciesState.Count == 0)
                return true;
            return !CurrenciesState.Select(x => x.Value).Any(x => !x.IsValid());
        }
    }
}
