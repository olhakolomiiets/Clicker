namespace CBS.Models
{
    public class FunctionGetClanLeaderboardRequest : FunctionBaseRequest
    {
        public string ClanID;
        public string StatisticName;
        public CBSClanConstraints Constraints;
        public int MaxCount;
        public int? Version;
        public int StartPostion;
    }
}
