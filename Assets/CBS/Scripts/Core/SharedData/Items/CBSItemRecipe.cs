using CBS.Core;

namespace CBS.Models
{
    public class CBSItemRecipe : CBSItemDependency, ICustomData<CBSRecipeCustomData>
    {
        public string ItemIdToGraft;

        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }
        public bool CompressCustomData => false;

        public T GetCustomData<T>() where T : CBSRecipeCustomData
        {
            if (CompressCustomData)
                return JsonPlugin.FromJsonDecompress<T>(CustomRawData);
            else
                return JsonPlugin.FromJson<T>(CustomRawData);
        }
    }
}
