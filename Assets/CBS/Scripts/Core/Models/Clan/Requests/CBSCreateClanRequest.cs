
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSCreateClanRequest
    {
        public string DisplayName;
        public string Description;
        public ClanVisibility Visibility;
        public Dictionary<string, string> CustomData;

        public ClanAvatarInfo AvatarInfo;
    }
}
