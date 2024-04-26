using CBS.Models;
using CBS.Utils;

namespace CBS.UI
{
    public class ClanTaskSlotUI : BaseTaskUI<CBSClanTask>
    {
        protected override string LockText => TasksTXTHandler.GetLockText(Task.LockLevel, Task.LevelFilter);
        protected override string NotCompleteText => TasksTXTHandler.NotCompleteText;
        protected override string CompleteText => TasksTXTHandler.CompleteText;

        private IClanTasks ClanTasks { get; set; }

        protected override void OnInit()
        {
            base.OnInit();
            ClanTasks = CBSModule.Get<CBSClanModule>();
        }

        protected override void DrawButtons(bool isLocked, bool isCompleted, bool rewardAvailable)
        {
            AddPointBt.SetActive(!isLocked && !isCompleted);
            LockBtn.SetActive(isLocked && !isCompleted);
            RewardBt.SetActive(false);
            CompleteBtn.SetActive(!isLocked && isCompleted);
            LevelIcon.SetActive(Task.Type == TaskType.TIERED);
        }

        // button click
        public override void OnAddPoint()
        {
            var achievementID = Task.ID;
            ClanTasks.AddTaskPoint(achievementID, onAdd =>
            {
                if (onAdd.IsSuccess)
                {
                    var updatedTask = onAdd.Task;
                    Display(updatedTask);
                }
                else
                {
                    new PopupViewer().ShowFabError(onAdd.Error);
                }
            });
        }

        public void ShowProfileReward()
        {
            var reward = Task.ProfileReward;
            new PopupViewer().ShowRewardPopup(reward);
        }
    }
}
