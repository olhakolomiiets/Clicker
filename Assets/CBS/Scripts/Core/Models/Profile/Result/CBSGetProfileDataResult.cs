using PlayFab.ClientModels;
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetProfileDataResult : CBSBaseResult
    {
        public Dictionary<string, UserDataRecord> Data;
    }
}