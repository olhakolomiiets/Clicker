using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSStartMatchResult
    {
        public string MatchID;
        public string TicketID;
        public string QueueName;
        public List<CBSMatchmakingPlayer> Players;
    }
}
