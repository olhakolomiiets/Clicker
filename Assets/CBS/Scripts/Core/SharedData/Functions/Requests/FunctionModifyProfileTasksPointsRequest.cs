namespace CBS.Models
{
    public class FunctionModifyProfileTasksPointsRequest : FunctionBaseRequest
    {
        public string TasksPoolID;
        public string TaskID;
        public int Points;
        public ModifyMethod Method;
        public int TimeZone;
    }
}
