#if CBS_IAP
using UnityEngine.Purchasing;
#endif

namespace CBS.Models
{
    public class IAPInitializeResult : CBSBaseResult
    {
        public string[] ProdutsIDs;
#if CBS_IAP
        public Product[] Products;
#endif
    }
}
