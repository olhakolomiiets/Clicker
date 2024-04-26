
using System;
using System.Text;

namespace CBS.Models
{
    public class CBSCurrency
    {
        public string Code;
        public int Value;
        public bool Rechargeable;
        public int MaxRecharge;
        public DateTime? RechargeTime;
        public int SecondsToRecharge;

        public int GetSecondsToNextRecharge()
        {
            if (!Rechargeable)
                return 0;
            var now = DateTime.UtcNow.ToLocalTime();
            var next = RechargeTime.GetValueOrDefault().ToLocalTime();
            var span = next.Subtract(now);
            return (int)span.TotalSeconds;
        }

        public bool IsMaxRecharge()
        {
            if (!Rechargeable)
                return false;
            return Value >= MaxRecharge;
        }

        public override string ToString()
        {
            if (Rechargeable)
            {
                var sBuilder = new StringBuilder(Value.ToString());
                sBuilder.Append("/");
                sBuilder.Append(MaxRecharge.ToString());
                return sBuilder.ToString();
            }
            return Value.ToString();
        }

        public static CBSCurrency Default(string code)
        {
            return new CBSCurrency
            {
                Code = code,
                Value = 0
            };
        }

        public static CBSCurrency Create(string code, uint value)
        {
            return new CBSCurrency
            {
                Code = code,
                Value = (int)value
            };
        }
    }
}
