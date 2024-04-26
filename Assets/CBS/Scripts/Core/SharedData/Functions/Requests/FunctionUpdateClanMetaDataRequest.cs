

namespace CBS.Models
{
    public class FunctionUpdateClanMetaDataRequest : FunctionBaseRequest
    {
        public string ClanID;

        public string DisplayName;
        public string Description;
        public ClanVisibility Visibility;
        public ClanAvatarInfo AvatarInfo;
    }
}
