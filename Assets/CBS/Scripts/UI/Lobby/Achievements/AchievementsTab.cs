using CBS.Models;
using UnityEngine;

namespace CBS.UI
{
    public class AchievementsTab : MonoBehaviour
    {
        [SerializeField]
        private TasksState TabType;

        public TasksState GetTabType()
        {
            return TabType;
        }
    }
}
