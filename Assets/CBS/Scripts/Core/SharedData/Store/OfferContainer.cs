using System.Collections.Generic;

namespace CBS.Models
{
    public class OfferContainer
    {
        public List<string> OfferIDs;

        public bool OfferPurchased(string offerInstanceID)
        {
            if (OfferIDs == null)
                return false;
            return OfferIDs.Contains(offerInstanceID);
        }

        public void AddOffer(string offerInstanceID)
        {
            if (string.IsNullOrEmpty(offerInstanceID))
                return;
            if (OfferIDs == null)
                OfferIDs = new List<string>();
            OfferIDs.Add(offerInstanceID);
        }
    }
}
