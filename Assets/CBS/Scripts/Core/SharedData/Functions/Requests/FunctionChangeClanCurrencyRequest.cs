namespace CBS.Models
{
    public class FunctionChangeClanCurrencyRequest : FunctionBaseRequest
    {
        public string ClanID;
        public string Code;
        public int Amount;
    }
}
