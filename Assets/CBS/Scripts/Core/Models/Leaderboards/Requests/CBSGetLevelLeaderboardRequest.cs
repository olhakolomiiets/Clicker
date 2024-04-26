namespace CBS.Models
{
    public class CBSGetLevelLeaderboardRequest
    {
        public CBSProfileConstraints Constraints;
        public int MaxCount;
        public int StartPosition;
        public int? Version;

        public CBSGetLeaderboardRequest ToLeaderboardRequest()
        {
            return new CBSGetLeaderboardRequest
            {
                StatisticName = StatisticKeys.PlayerExpirienceStatistic,
                Constraints = this.Constraints,
                MaxCount = this.MaxCount,
                StartPosition = this.StartPosition,
                Version = this.Version
            };
        }
    }
}
