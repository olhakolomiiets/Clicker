using CBS.Core;
using System;

namespace CBS.Models
{
    public class EventMetaData : ICustomData<CBSEventsCustomData>
    {
        public string ID;
        public string Category;
        public EventDurationType DurationType;
        public EventExecuteType ExecuteType;
        public int DurationInSeconds;
        public bool CustomCron;
        public TaskEventContainer StartTasks;
        public TaskEventContainer EndTasks;
        public bool IsRunning;
        public bool InProccess;
        public string InstanceID;
        public DateTime? StartDate;
        public DateTime? EndDate;

        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }
        public bool CompressCustomData => false;

        public T GetCustomData<T>() where T : CBSEventsCustomData
        {
            if (CompressCustomData)
                return JsonPlugin.FromJsonDecompress<T>(CustomRawData);
            else
                return JsonPlugin.FromJson<T>(CustomRawData);
        }
    }
}