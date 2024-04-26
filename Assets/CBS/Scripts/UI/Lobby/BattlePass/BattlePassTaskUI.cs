using CBS.Models;
using CBS.Utils;

namespace CBS.UI
{
    public class BattlePassTaskUI : BaseTaskUI<CBSProfileTask>
    {
        protected override string LockText => TasksTXTHandler.GetLockText(Task.LockLevel, Task.LevelFilter);
        protected override string NotCompleteText => TasksTXTHandler.NotCompleteText;
        protected override string CompleteText => TasksTXTHandler.CompleteText;

        private IBattlePass BattlePass { get; set; }
        private string BattlePassID { get; set; }

        protected override void OnInit()
        {
            base.OnInit();
            BattlePass = CBSModule.Get<CBSBattlePassModule>();
        }

        public void SetBattlePassID(string id) => BattlePassID = id;

        public override void OnAddPoint()
        {
            BattlePass.AddBattlePassTaskPoints(BattlePassID, Task.ID, 1, onAdd =>
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
            BattlePass.PickupBattlePassTaskReward(BattlePassID, taskID, onPick =>
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
