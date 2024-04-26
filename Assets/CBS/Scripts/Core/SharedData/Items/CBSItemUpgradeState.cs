using CBS.Core;

namespace CBS.Models
{
    public class CBSItemUpgradeState : CBSItemDependency, ICustomData<CBSUpgradeItemCustomData>
    {
        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }
        public bool CompressCustomData => false;

        public T GetCustomData<T>() where T : CBSUpgradeItemCustomData
        {
            if (CompressCustomData)
                return JsonPlugin.FromJsonDecompress<T>(CustomRawData);
            else
                return JsonPlugin.FromJson<T>(CustomRawData);
        }
    }
}
