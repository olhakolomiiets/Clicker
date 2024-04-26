using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetProfilesResult : CBSBaseResult
    {
        public Dictionary<string, ProfileEntity> Profiles;
    }
}
