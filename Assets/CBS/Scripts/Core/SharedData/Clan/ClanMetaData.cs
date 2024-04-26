
using System.Collections.Generic;
using System.Linq;

namespace CBS.Models
{
    public class ClanMetaData
    {
        private static readonly int MaxClanMemberCount = 100;
        private static readonly int DefaultClanMemberCount = 20;

        public static readonly string AdminRoleID = "admins";
        public static readonly string MemberRoleID = "members";

        private static readonly string DefaultAdminDisplayName = "Owner";
        private static readonly string DefaultMemberDisplayName = "Member";

        public static readonly string JoinMessageTemplate = "Player {0} has joined the clan";
        public static readonly string LeftMessageTemplate = "Player {0} has left the clan";
        public static readonly string RoleMessageTemplate = "The player role of {0} has been changed to {1}";

        public int ClanMemberCount;
        public bool SendJoinMessage;
        public bool SendLeaveMessage;
        public bool SendChangeRoleMessage;
        public bool DisplayNameProfanityCheck;
        public ClanLevelTable LevelTable;
        public List<ClanRoleInfo> RoleList;

        public int GetClanMemberCount()
        {
            if (ClanMemberCount <= 0)
                return DefaultClanMemberCount;
            else if (ClanMemberCount > MaxClanMemberCount)
                return MaxClanMemberCount;
            return ClanMemberCount;
        }

        public ClanLevelTable GetLevelTable()
        {
            if (LevelTable == null)
                LevelTable = new ClanLevelTable();
            return LevelTable;
        }

        public List<ClanRoleInfo> GetRoleList()
        {
            if (RoleList == null || RoleList.Count == 0)
            {
                RoleList = GetDefaultRoleList();
            }
            return RoleList;
        }

        public List<ClanRoleInfo> GetCustomRoleList()
        {
            var roleList = GetRoleList();
            return roleList.Where(x => x.RoleID != AdminRoleID && x.RoleID != MemberRoleID).ToList();
        }

        private List<ClanRoleInfo> GetDefaultRoleList()
        {
            var roleList = new List<ClanRoleInfo>();
            roleList.Add(new ClanRoleInfo
            {
                RoleID = AdminRoleID,
                DisplayName = DefaultAdminDisplayName
            });
            roleList.Add(new ClanRoleInfo
            {
                RoleID = MemberRoleID,
                DisplayName = DefaultMemberDisplayName
            });
            return roleList;
        }

        public void AddNewRole(string roleID)
        {
            if (string.IsNullOrEmpty(roleID))
                return;
            if (RoleList == null)
                return;
            var containRole = RoleList.Any(x => x.RoleID == roleID);
            if (containRole)
                return;
            RoleList.Insert(1, new ClanRoleInfo
            {
                RoleID = roleID
            });
        }

        public void RemoveRole(string roleID)
        {
            if (string.IsNullOrEmpty(roleID))
                return;
            if (RoleList == null)
                return;
            var containRole = RoleList.Any(x => x.RoleID == roleID);
            if (!containRole)
                return;
            if (IsDefaultRole(roleID))
                return;
            var roleInfo = RoleList.FirstOrDefault(x => x.RoleID == roleID);
            RoleList.Remove(roleInfo);
            RoleList.TrimExcess();
        }

        public void MoveRoleUp(string roleID)
        {
            if (string.IsNullOrEmpty(roleID))
                return;
            if (RoleList == null)
                return;
            var containRole = RoleList.Any(x => x.RoleID == roleID);
            if (!containRole)
                return;
            var roleInfo = RoleList.FirstOrDefault(x => x.RoleID == roleID);
            var oldRoleIndex = RoleList.IndexOf(roleInfo);
            if (oldRoleIndex <= 1)
                return;
            oldRoleIndex--;
            RoleList.Remove(roleInfo);
            RoleList.TrimExcess();
            RoleList.Insert(oldRoleIndex, roleInfo);
        }

        public void MoveRoleDown(string roleID)
        {
            if (string.IsNullOrEmpty(roleID))
                return;
            if (RoleList == null)
                return;
            var containRole = RoleList.Any(x => x.RoleID == roleID);
            if (!containRole)
                return;
            var roleInfo = RoleList.FirstOrDefault(x => x.RoleID == roleID);
            var oldRoleIndex = RoleList.IndexOf(roleInfo);
            if (oldRoleIndex >= RoleList.Count - 2)
                return;
            oldRoleIndex++;
            RoleList.Remove(roleInfo);
            RoleList.TrimExcess();
            RoleList.Insert(oldRoleIndex, roleInfo);
        }

        public bool IsDefaultRole(string roleID)
        {
            return roleID == AdminRoleID || roleID == MemberRoleID;
        }

        public bool IsAdminRole(string roleID)
        {
            return roleID == AdminRoleID;
        }

        public bool HasPermissionForAction(string roleID, ClanRolePermission permission)
        {
            if (IsAdminRole(roleID))
                return true;
            if (RoleList == null)
                return false;
            var containRole = RoleList.Any(x => x.RoleID == roleID);
            if (!containRole)
                return false;
            var roleInfo = RoleList.FirstOrDefault(x => x.RoleID == roleID);
            var permissionList = roleInfo.RolePermissions;
            if (permissionList == null)
                return false;
            return permissionList.Contains(permission);
        }

        public void RemovePermissionForAction(string roleID, ClanRolePermission permission)
        {
            if (IsAdminRole(roleID))
                return;
            if (RoleList == null)
                return;
            var containRole = RoleList.Any(x => x.RoleID == roleID);
            if (!containRole)
                return;
            var roleInfo = RoleList.FirstOrDefault(x => x.RoleID == roleID);
            var permissionList = roleInfo.RolePermissions ?? new List<ClanRolePermission>();
            if (permissionList.Contains(permission))
            {
                permissionList.Remove(permission);
                permissionList.TrimExcess();
            }
            roleInfo.RolePermissions = permissionList;
        }

        public void AddPermissionForAction(string roleID, ClanRolePermission permission)
        {
            if (IsAdminRole(roleID))
                return;
            if (RoleList == null)
                return;
            var containRole = RoleList.Any(x => x.RoleID == roleID);
            if (!containRole)
                return;
            var roleInfo = RoleList.FirstOrDefault(x => x.RoleID == roleID);
            var permissionList = roleInfo.RolePermissions ?? new List<ClanRolePermission>();
            if (!permissionList.Contains(permission))
            {
                permissionList.Add(permission);
            }
            roleInfo.RolePermissions = permissionList;
        }
    }
}
