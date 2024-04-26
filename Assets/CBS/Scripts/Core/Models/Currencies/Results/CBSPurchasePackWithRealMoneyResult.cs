using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSPurchasePackWithRealMoneyResult : CBSBaseResult
    {
        public string TransactionID;
        public string ProfileID;
        public Dictionary<string, uint> GrantedCurrencies;
    }
}
