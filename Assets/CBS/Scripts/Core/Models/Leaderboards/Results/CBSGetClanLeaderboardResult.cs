using System;
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetClanLeaderboardResult : CBSBaseResult
    {
        public List<ClanLeaderboardEntry> Leaderboard;
        public ClanLeaderboardEntry ClanEntry;
        public DateTime? NextReset;
        public int Version;
    }
}
