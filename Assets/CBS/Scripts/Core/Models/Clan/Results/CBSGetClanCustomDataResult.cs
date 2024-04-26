using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetClanCustomDataResult : CBSBaseResult
    {
        public string ClanID;
        public Dictionary<string, string> CustomData;
    }
}
