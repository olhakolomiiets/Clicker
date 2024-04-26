using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class ProfileOffers
    {
        public List<ProfileOfferInfo> Offers;

        public ProfileOfferInfo GetOfferByItemID(string itemID)
        {
            if (Offers == null)
                return null;
            return Offers.FirstOrDefault(x => x.ItemID == itemID);
        }

        public bool OfferExist(string offerInstanceID)
        {
            if (Offers == null)
                return false;
            return Offers.Any(x => x.OfferInstanceID == offerInstanceID);
        }

        public bool IsActive(string offerInstanceID, DateTime serverTime)
        {
            if (!OfferExist(offerInstanceID))
                return false;
            var offer = Offers.FirstOrDefault(x => x.OfferInstanceID == offerInstanceID);
            var endDate = offer.EndDate;
            if (endDate == null)
            {
                return true;
            }
            else
            {
                return endDate.GetValueOrDefault().Subtract(serverTime).Ticks > 0;
            }
        }

        public void AddOffer(ProfileOfferInfo offer)
        {
            Offers = Offers ?? new List<ProfileOfferInfo>();
            Offers.Add(offer);
        }

        public void RemoveOutDated(DateTime serverTime)
        {
            if (Offers == null)
                return;
            Offers = Offers.Where(x => IsActive(x.OfferInstanceID, serverTime)).ToList();
            Offers.TrimExcess();
        }

        public void RemoveOffer(string itemID)
        {
            if (Offers == null)
                return;
            Offers = Offers.Where(x => x.ItemID != itemID).ToList();
            Offers.TrimExcess();
        }
    }
}
