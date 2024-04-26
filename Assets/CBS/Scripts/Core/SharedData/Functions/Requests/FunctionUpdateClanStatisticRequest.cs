

namespace CBS.Models
{
    public class FunctionUpdateClanStatisticRequest : FunctionBaseRequest
    {
        public string ClanID;
        public string StatisticName;
        public int StatisticValue;
    }
}

