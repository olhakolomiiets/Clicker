using CBS.Models;

namespace CBS.Utils
{
    public class AchievementsTXTHandler
    {
        public const string NotCompleteText = "Achievement not yet completed";
        public const string CompleteText = "Achievement completed!";

        public static string GetLockText(int level, IntFilter filter)
        {
            if (filter == IntFilter.EQUAL_OR_GREATER)
            {
                return string.Format("Achievement is available at the level {0}", level);
            }
            else if (filter == IntFilter.EQUAL_OR_LESS)
            {
                return string.Format("Achievements available for level {0} or below", level);
            }
            else if (filter == IntFilter.EQUAL)
            {
                return string.Format("Achievements only available at level {0}", level);
            }
            return string.Empty;
        }
    }
}
