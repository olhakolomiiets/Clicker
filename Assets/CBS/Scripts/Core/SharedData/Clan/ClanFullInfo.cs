
using System.Collections.Generic;

namespace CBS.Models
{
    public class ClanFullInfo
    {
        public string ClanID;
        public string ClanGroupID;
        public string DisplayName;
        public string Description;

        public int MembersCount;

        public LevelInfo LevelInfo;
        public ClanAvatarInfo AvatarInfo;
        public ClanVisibility Visibility;
        public List<ClanRoleInfo> RolesList;
        public Dictionary<string, string> CustomData;
    }
}
