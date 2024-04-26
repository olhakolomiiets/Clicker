
using CBS.Core;

namespace CBS.Models
{
    public class CBSTitleData : ICustomData<TitleCustomData>
    {
        public string DataKey;
        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }

        public bool CompressCustomData => false;

        public T GetCustomData<T>() where T : TitleCustomData
        {
            return JsonPlugin.FromJson<T>(CustomRawData);
        }
    }
}
