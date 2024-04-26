#if ENABLE_PLAYFABADMIN_API
using CBS.Editor.Window;

namespace CBS.Editor
{
    public class AchievementsConfigurator : BaseTasksConfigurator<CBSTask, AchievementsData, AddAchievementWindow>
    {
        protected override string Title => "Achievements";

        protected override string TASK_TITLE_ID => TitleKeys.AchievementsTitleKey;

        protected override string[] Titles => new string[] { "Achievements", "Additional configs" };

        protected override string ItemKey => "task";
    }
}
#endif
