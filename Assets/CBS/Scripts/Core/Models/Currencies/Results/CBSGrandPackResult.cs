using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGrandPackResult : CBSBaseResult
    {
        public string ProfileID;
        public Dictionary<string, uint> GrantedCurrencies;
    }
}
