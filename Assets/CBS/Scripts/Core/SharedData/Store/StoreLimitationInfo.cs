
using System;

namespace CBS.Models
{
    public class StoreLimitationInfo
    {
        public DatePeriod LimitPeriod;
        public int MaxQuantity;
        public int LeftQuantity;
        public DateTime? ResetLimitDate;
    }
}
