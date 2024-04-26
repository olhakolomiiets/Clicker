
#if CBS_IAP
using UnityEngine.Purchasing;
#endif

namespace CBS.Models
{
    [System.Serializable]
    public class CBSExternalProduct
    {
        public string ProductID;
#if CBS_IAP
        public ProductType Type;
#endif
    }
}
