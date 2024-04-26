using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionIAPPostPurchaseResult
    {
        public string ProfileID;
        public Dictionary<string, uint> GrantedCurrencies;
        public StoreLimitationInfo LimitationInfo;
    }
}
