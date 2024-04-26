namespace CBS.Models
{
    public class ClanLeaderboardEntry : ClanEntity
    {
        public int StatisticPosition;
        public int StatisticValue;

        public static ClanLeaderboardEntry FromClanEntity(ClanEntity clan, int position, int value)
        {
            var leaderboardEntry = clan == null ? new ClanLeaderboardEntry() : JsonPlugin.FromJson<ClanLeaderboardEntry>(JsonPlugin.ToJson(clan));
            leaderboardEntry.StatisticPosition = position;
            leaderboardEntry.StatisticValue = value;
            return leaderboardEntry;
        }
    }
}
