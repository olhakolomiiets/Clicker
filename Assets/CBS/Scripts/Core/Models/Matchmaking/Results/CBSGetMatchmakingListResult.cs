using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetMatchmakingListResult : CBSBaseResult
    {
        public List<CBSMatchmakingQueue> Queues;
    }
}
