namespace CBS.Models
{
    public class CBSGetProfileResult : CBSBaseResult
    {
        public string ProfileID;
        public string DisplayName;
        public string ClanID;
        public AvatarInfo Avatar;
        public EntityLevelInfo Level;
        public StatisticsInfo Statistics;
        public DataInfo ProfileData;
        public OnlineStatusData OnlineStatus;
        public ClanEntity ClanEntity;
    }
}
