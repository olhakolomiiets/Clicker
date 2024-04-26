namespace CBS.Models
{
    public class CBSUpdateCurrencyResult : CBSBaseResult
    {
        public string TargetID;
        public int BalanceChange;
        public CBSCurrency UpdatedCurrency;
    }
}
