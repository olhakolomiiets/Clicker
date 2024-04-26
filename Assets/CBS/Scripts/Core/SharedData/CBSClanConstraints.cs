namespace CBS.Models
{
    public class CBSClanConstraints
    {
        public bool LoadAvatar;
        public bool LoadLevel;
        public bool LoadStatistics;
        public bool LoadClanData;
        public bool LoadMembersCount;
        public bool LoadVisibility;
        public bool LoadDescription;

        public static CBSClanConstraints Full()
        {
            return new CBSClanConstraints
            {
                LoadAvatar = true,
                LoadLevel = true,
                LoadStatistics = true,
                LoadClanData = true,
                LoadMembersCount = true,
                LoadVisibility = true,
                LoadDescription = true
            };
        }

        public static CBSClanConstraints Default()
        {
            return new CBSClanConstraints
            {
                LoadAvatar = true,
                LoadMembersCount = true,
                LoadVisibility = true,
            };
        }
    }
}
