using System;
using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionGetClanLeaderboardResult
    {
        public List<ClanLeaderboardEntry> Leaderboard;
        public ClanLeaderboardEntry ClanEntry;
        public DateTime? NextReset;
        public int Version;
    }
}

