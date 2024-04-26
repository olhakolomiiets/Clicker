#if ENABLE_PLAYFABADMIN_API
using CBS.Core;
using PlayFab.AdminModels;

namespace CBS
{
    public class ItemCustomDataAdapter<TBaseType> : ICustomData<TBaseType> where TBaseType : CBSBaseCustomData
    {
        private CatalogItem Item;

        public ItemCustomDataAdapter(CatalogItem item)
        {
            Item = item;
        }

        public string CustomDataClassName
        {
            get => Item.ItemClass;
            set => Item.ItemClass = value;
        }
        public string CustomRawData
        {
            get => Item.CustomData;
            set => Item.CustomData = value;
        }

        public bool CompressCustomData => false;

        public T GetCustomData<T>() where T : TBaseType
        {
            if (CompressCustomData)
                return JsonPlugin.FromJsonDecompress<T>(CustomRawData);
            else
                return JsonPlugin.FromJson<T>(CustomRawData);
        }
    }
}
#endif
