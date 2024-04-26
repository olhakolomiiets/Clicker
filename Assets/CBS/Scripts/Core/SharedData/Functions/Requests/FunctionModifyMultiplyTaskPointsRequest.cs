
using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionModifyMultiplyTaskPointsRequest
    {
        public string EntityID;
        public CBSEntityType EntityType;
        public ModifyMethod ModifyMethod;
        public Dictionary<string, int> ModifyPair;
        public string TasksTitleID;
        public string TasksEntityTitleID;
    }
}
