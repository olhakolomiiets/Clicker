
namespace CBS.Models
{
    public class ProfileEntity
    {
        public string ProfileID;
        public string DisplayName;
        public AvatarInfo Avatar;
        public string ClanID;
        public EntityLevelInfo Level;
        public StatisticsInfo Statistics;
        public DataInfo ProfileData;
        public OnlineStatusData OnlineStatus;
        public ClanEntity ClanEntity;

        public bool IsOnline()
        {
            if (OnlineStatus == null)
                return false;
            return OnlineStatus.IsOnline;
        }
    }
}
