using CBS.Models;
using CBS.Utils;

namespace CBS.UI
{
    public class ProfileTasksUI : BaseTaskUI<CBSProfileTask>
    {
        protected override string LockText => TasksTXTHandler.GetLockText(Task.LockLevel, Task.LevelFilter);
        protected override string NotCompleteText => TasksTXTHandler.NotCompleteText;
        protected override string CompleteText => TasksTXTHandler.CompleteText;

        private IProfileTasks ProfileTask { get; set; }

        protected override void OnInit()
        {
            base.OnInit();
            ProfileTask = CBSModule.Get<CBSProfileTasksModule>();
        }

        public override void OnAddPoint()
        {
            ProfileTask.AddTaskPoints(Task, 1, onAdd =>
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

        public override void GetRewards()
        {
            var taskID = Task.ID;
            var poolID = Task.PoolID;
            ProfileTask.PickupTaskReward(poolID, taskID, onPick =>
            {
                if (onPick.IsSuccess)
                {
                    var updatedTask = onPick.Task;
                    Display(updatedTask);
                }
                else
                {
                    new PopupViewer().ShowFabError(onPick.Error);
                }
            });
        }
    }
}
