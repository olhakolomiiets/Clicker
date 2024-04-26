namespace CBS.Models
{
    public class ProfileLeaderboardEntry : ProfileEntity
    {
        public int StatisticPosition;
        public int StatisticValue;

        public static ProfileLeaderboardEntry FromProfileEntity(ProfileEntity profile, int position, int value)
        {
            var leaderboardEntry = profile == null ? new ProfileLeaderboardEntry() : JsonPlugin.FromJson<ProfileLeaderboardEntry>(JsonPlugin.ToJson(profile));
            leaderboardEntry.StatisticPosition = position;
            leaderboardEntry.StatisticValue = value;
            return leaderboardEntry;
        }
    }
}
