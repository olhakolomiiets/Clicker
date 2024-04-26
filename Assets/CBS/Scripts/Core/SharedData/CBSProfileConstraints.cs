namespace CBS.Models
{
    public class CBSProfileConstraints
    {
        public bool LoadAvatar;
        public bool LoadClan;
        public bool LoadLevel;
        public bool LoadStatistics;
        public bool LoadOnlineStatus;
        public bool LoadProfileData;

        public static CBSProfileConstraints Full()
        {
            return new CBSProfileConstraints
            {
                LoadAvatar = true,
                LoadClan = true,
                LoadLevel = true,
                LoadOnlineStatus = true,
                LoadProfileData = true,
                LoadStatistics = true
            };
        }

        public static CBSProfileConstraints Default()
        {
            return new CBSProfileConstraints
            {
                LoadAvatar = true,
                LoadClan = true,
                LoadLevel = true,
                LoadOnlineStatus = true
            };
        }
    }
}
