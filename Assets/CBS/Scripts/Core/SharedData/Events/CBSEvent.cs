using CBS.Core;
using System;

namespace CBS.Models
{
    public class CBSEvent : ICustomData<CBSEventsCustomData>
    {
        public string ID;
        public string InstanceID;
        public string DisplayName;
        public string Description;
        public string CronExpression;
        public string Category;

        public bool IsRunning;

        public DateTime? LastRunTime;
        public DateTime? NextRunTime;
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
