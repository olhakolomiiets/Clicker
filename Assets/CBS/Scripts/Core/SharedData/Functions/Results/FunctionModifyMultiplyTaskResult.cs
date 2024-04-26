

using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionModifyMultiplyTaskResult<T> where T : CBSTask
    {
        public Dictionary<string, FunctionModifyTaskResult<T>> TasksResults;
    }
}
