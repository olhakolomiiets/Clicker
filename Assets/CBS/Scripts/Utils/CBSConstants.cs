namespace CBS
{
    public class CBSConstants
    {
        // items
        public const string UndefinedCategory = "undefined";
        public const int MaxItemsCountForRecipe = 100;

        // chat
        public const string UnknownName = "Unknown";
        public const string ChatGlobalID = "CBSGlobalChat";
        public const string ChatDefaultRegion = "en";
        public const string ChatDefaultServer = "CBSDefaultServer";
        public const int ChatCompareCount = 10;
        public const float ChatCompareWait = 500; // miliseconds
        public const int MaxChatHistory = 100;

        // tournaments
        public const string TournamentsDataKey = "CBSTournaments";
        // entities
        public const string EntityPlayerType = "title_player_account";
        // matchmaking
        public const int MatchmakingDefaultWaitTime = 120; // in seconds
        public const int MatchmakingRefreshTime = 6; // in seconds
        public const string MatchmakingLevelEqualityAttribute = "LevelEquality";
        public const string MatchmakingLevelDifferenceAttribute = "LevelDifference";
        public const string MatchmakingValueDifferenceAttribute = "ValueDifference";
        public const string MatchmakingStringEqualityAttribute = "MatchmakingStringEquality";
        public const string LevelEqualityRuleName = "CBSLevelEquality";
        public const string StringEqualityRuleName = "CBSStringEquality";
        public const string LevelDifferenceRuleName = "CBSLevelDifference";
        public const string ValueDifferenceRuleName = "CBSValueDifference";
    }
}
