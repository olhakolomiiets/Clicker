using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionCurrenciesResult
    {
        public string TargetID;
        public Dictionary<string, CBSCurrency> Currencies;
    }
}
