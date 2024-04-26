namespace CBS.Models
{
    public class FunctionModifyClanTasksPointsRequest : FunctionBaseRequest
    {
        public string ClanID;
        public string TasksPoolID;
        public string TaskID;
        public int Points;
        public ModifyMethod Method;
    }
}
