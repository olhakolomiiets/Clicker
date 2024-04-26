
using System;

namespace CBS.Models
{
    public class CBSFindMatchResult : CBSBaseResult
    {
        public string TicketID;
        public DateTime CreatedDate;
        public CBSMatchmakingQueue Queue;
    }
}
