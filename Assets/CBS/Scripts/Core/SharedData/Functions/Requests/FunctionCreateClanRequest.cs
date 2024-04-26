using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionCreateClanRequest : FunctionBaseRequest
    {
        public string EntityID;
        public string DisplayName;
        public string Description;
        public ClanVisibility Visibility;
        public Dictionary<string, string> CustomData;

        public ClanAvatarInfo AvatarInfo;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(DisplayName) && !string.IsNullOrEmpty(ProfileID) && !string.IsNullOrEmpty(EntityID);
        }
    }
}
