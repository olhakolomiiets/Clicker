using System.Collections.Generic;

namespace CBS.Models
{
    public class ClanRoleInfo
    {
        public string RoleID;
        public string DisplayName;
        public List<ClanRolePermission> RolePermissions;
    }
}
