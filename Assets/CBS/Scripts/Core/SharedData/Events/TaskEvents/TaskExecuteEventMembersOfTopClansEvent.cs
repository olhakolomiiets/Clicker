
namespace CBS.Models
{
    public class TaskExecuteEventMembersOfTopClansEvent : TaskEvent
    {
        public ProfileEventContainer Events;
        public int nTop;
        public string StatisticName;
    }
}
