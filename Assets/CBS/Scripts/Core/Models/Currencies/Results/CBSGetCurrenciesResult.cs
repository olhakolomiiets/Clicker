using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetCurrenciesResult : CBSBaseResult
    {
        public string TargetID;
        public Dictionary<string, CBSCurrency> Currencies;
    }
}