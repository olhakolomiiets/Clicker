namespace CBS.Utils
{
    public class TournamnetTXTHandler
    {
        public const string WarningTitle = "Warning";
        public const string LeaveWarning = "Are you sure you want to leave the tournament";

        public static string GetPlaceText(int position)
        {
            return "You finished in " + position.ToString() + " place";
        }
    }
}