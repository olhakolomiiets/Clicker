using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionPayForDependencyResult
    {
        public List<string> SpendedInstanesIDs;
        public Dictionary<string, uint> SpendedCurrencies;
        public Dictionary<string, uint> ConsumedItems;
    }
}
