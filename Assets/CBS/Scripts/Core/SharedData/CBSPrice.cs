
namespace CBS.Models
{
    public class CBSPrice
    {
        public string CurrencyID;
        public int CurrencyValue;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(CurrencyID);
        }
    }
}
