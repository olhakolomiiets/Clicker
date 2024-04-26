using System;

namespace CBS.Models
{
    public class CBSSpecialOffer : CBSStoreItem
    {
        public string OfferInstanceID;
        public bool HasTimeLimit;
        public int Duration;
        public DateTime? OfferEndDate;
    }
}
