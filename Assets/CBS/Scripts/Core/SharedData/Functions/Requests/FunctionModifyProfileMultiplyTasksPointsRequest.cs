using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionModifyProfileMultiplyTasksPointsRequest : FunctionBaseRequest
    {
        public string TasksPoolID;
        public Dictionary<string, int> ModifyPair;
        public ModifyMethod Method;
        public int TimeZone;
    }
}
