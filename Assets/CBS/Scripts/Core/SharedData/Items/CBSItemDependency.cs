using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSItemDependency
    {
        public Dictionary<string, uint> CurrencyDependecies;
        public Dictionary<string, uint> ItemsDependencies;

        public bool IsEmpty()
        {
            return (CurrencyDependecies == null || CurrencyDependecies.Count == 0) && (ItemsDependencies == null | ItemsDependencies.Count == 0);
        }
    }
}
