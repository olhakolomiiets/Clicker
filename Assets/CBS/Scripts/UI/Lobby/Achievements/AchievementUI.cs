using CBS.Utils;

namespace CBS.UI
{
    public class AchievementUI : BaseTaskUI<CBSTask>
    {
        protected override string LockText => AchievementsTXTHandler.GetLockText(Task.LockLevel, Task.LevelFilter);
        protected override string NotCompleteText => AchievementsTXTHandler.NotCompleteText;
        protected override string CompleteText => AchievementsTXTHandler.CompleteText;

        private IAchievements Achievements { get; set; }

        protected override void OnInit()
        {
            base.OnInit();
            Achievements = CBSModule.Get<CBSAchievementsModule>();
        }

        // button click
        public override void OnAddPoint()
        {
            var achievementID = Task.ID;
            Achievements.AddAchievementPoint(achievementID, 1, onAdd =>
            {
                if (onAdd.IsSuccess)
                {
                    var updatedAchievement = onAdd.Achievement;
                    Display(updatedAchievement);
                }
                else
                {
                    new PopupViewer().ShowFabError(onAdd.Error);
                }
            });
        }

        public void ResetAchievement()
        {
            var achievementID = Task.ID;
            Achievements.ResetAchievement(achievementID, onReset =>
            {
                if (onReset.IsSuccess)
                {
                    var updatedAchievement = onReset.Achievement;
                    Display(updatedAchievement);
                }
            });
        }

        public override void GetRewards()
        {
            var achievementID = Task.ID;
            Achievements.PickupAchievementReward(achievementID, onPick =>
            {
                if (onPick.IsSuccess)
                {
                    var updatedAchievement = onPick.Achievement;
                    Display(updatedAchievement);
                }
                else
                {
                    new PopupViewer().ShowFabError(onPick.Error);
                }
            });
        }
    }
}
