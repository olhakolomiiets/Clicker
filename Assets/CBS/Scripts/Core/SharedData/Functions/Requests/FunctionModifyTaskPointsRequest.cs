
namespace CBS.Models
{
    public class FunctionModifyTaskPointsRequest
    {
        public string EntityID;
        public CBSEntityType EntityType;
        public ModifyMethod ModifyMethod;
        public int Points;
        public string TaskID;
        public string TasksTitleID;
        public string TasksEntityTitleID;
    }
}
