
using System;

namespace CBS.Models
{
    public class FunctionStartDurableTaskResult
    {
        public string DurableTaskInstanceID;
        public string EventID;
        public DateTime ExecuteDate;
    }
}