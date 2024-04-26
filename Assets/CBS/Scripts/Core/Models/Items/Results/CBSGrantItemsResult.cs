using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGrantItemsResult : CBSBaseResult
    {
        public string TargetID;
        public List<CBSInventoryItem> GrantedInstances;
        public Dictionary<string, uint> GrantedCurrencies;
    }
}
