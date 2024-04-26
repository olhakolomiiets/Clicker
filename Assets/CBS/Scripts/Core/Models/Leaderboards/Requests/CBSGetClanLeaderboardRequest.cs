namespace CBS.Models
{
    public class CBSGetClanLeaderboardRequest
    {
        public string StatisticName;
        public CBSClanConstraints Constraints;
        public int MaxCount;
        public int StartPosition;
        public int? Version;
    }
}
