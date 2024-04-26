using System;

namespace CBS
{
    public class Period
    {
        public DateTime Start = DateTime.Now;
        public DateTime End = DateTime.Now.AddMonths(1);
    }
}
