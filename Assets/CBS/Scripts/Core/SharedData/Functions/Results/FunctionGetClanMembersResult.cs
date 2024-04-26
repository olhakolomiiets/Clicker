using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionGetClanMembersResult
    {
        public string ClanID;
        public List<ClanRoleInfo> AvailableRoles;
        public List<ClanMember> Members;
    }
}
