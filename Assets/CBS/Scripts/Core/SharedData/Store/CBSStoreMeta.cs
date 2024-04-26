

using CBS.Core;

namespace CBS.Models
{
    public class CBSStoreMeta : ICustomData<CBSStoreCustomData>
    {
        public bool Enable;
        public bool HasClanLimit;
        public bool HasLevelLimit;
        public int LevelLimit;
        public IntFilter LevelFilter;
        public bool HasStatisticLimit;
        public string StatisticLimitName;
        public int StatisticLimitValue;
        public IntFilter StatisticFilter;

        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }

        public bool CompressCustomData => false;

        public T GetCustomData<T>() where T : CBSStoreCustomData
        {
            return JsonPlugin.FromJson<T>(CustomRawData);
        }
    }
}
