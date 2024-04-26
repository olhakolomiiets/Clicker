using System;
using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionGetLeaderboardResult
    {
        public List<ProfileLeaderboardEntry> Leaderboard;
        public ProfileLeaderboardEntry ProfileEntry;
        public DateTime? NextReset;
        public int Version;
    }
}

