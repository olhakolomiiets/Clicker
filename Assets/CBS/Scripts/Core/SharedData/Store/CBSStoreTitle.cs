

using CBS.Core;

namespace CBS.Models
{
    public class CBSStoreTitle : ICustomData<CBSStoreCustomData>
    {
        public string ID;
        public string DisplayName;
        public string Description;

        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }
        public bool CompressCustomData => false;

        public T GetCustomData<T>() where T : CBSStoreCustomData
        {
            return JsonPlugin.FromJson<T>(CustomRawData);
        }
    }
}
