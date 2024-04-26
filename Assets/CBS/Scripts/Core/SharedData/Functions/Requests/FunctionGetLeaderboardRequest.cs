namespace CBS.Models
{
    public class FunctionGetLeaderboardRequest : FunctionBaseRequest
    {
        public string StatisticName;
        public CBSProfileConstraints Constraints;
        public int MaxCount;
        public int? Version;
        public int StartPostion;
    }
}
