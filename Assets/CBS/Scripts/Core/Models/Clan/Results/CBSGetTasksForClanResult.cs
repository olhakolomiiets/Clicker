using System;
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetTasksForClanResult : CBSBaseResult
    {
        public List<CBSClanTask> Tasks;
        public DateTime? ResetDate;
    }
}
