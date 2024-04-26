
using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionUpdateCustomDataRequest : FunctionBaseRequest
    {
        public string ClanID;
        public Dictionary<string, string> UpdateRequest;
    }
}
