namespace CBS.Models
{
    public class CBSGetLeaderboardRequest
    {
        public string StatisticName;
        public CBSProfileConstraints Constraints;
        public int MaxCount;
        public int StartPosition;
        public int? Version;
    }
}
