using System;
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetTasksForProfileResult : CBSBaseResult
    {
        public List<CBSProfileTask> Tasks;
        public DateTime? ResetDate;
    }
}
