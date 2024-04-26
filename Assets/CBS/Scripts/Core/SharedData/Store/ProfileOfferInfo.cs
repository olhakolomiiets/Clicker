using System;

namespace CBS.Models
{
    public class ProfileOfferInfo
    {
        public string OfferInstanceID;
        public string StoreID;
        public string ItemID;
        public DateTime? EndDate;

        public bool IsActive(DateTime serverDate)
        {
            if (EndDate == null)
                return true;
            var endDate = EndDate.GetValueOrDefault();
            var span = endDate.Subtract(serverDate);
            return span.Ticks > 0;
        }
    }
}
