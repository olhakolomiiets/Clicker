namespace CBS.Models
{
    public class FunctionModifyAchievementPointsRequest : FunctionBaseRequest
    {
        public string AchievementID;
        public int Points;
        public ModifyMethod Method;
    }
}
