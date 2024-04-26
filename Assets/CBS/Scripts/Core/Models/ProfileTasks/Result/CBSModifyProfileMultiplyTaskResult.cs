
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSModifyProfileMultiplyTaskResult : CBSBaseResult
    {
        public Dictionary<string, CBSModifyProfileTaskPointsResult> TasksResults;
    }
}
