using System;

namespace CBS.Models
{
    public class CBSInAppPurchaseQueue
    {
        public string ItemID;
        public string CatalogID;
        public string TransactionID;
        public string StoreID;
        public Action<IAPPurchaseItemResult> Result;

        public bool IsPack()
        {
            if (CatalogID == CatalogKeys.CurrencyCatalogID)
            {
                return true;
            }
            else if (CatalogID == CatalogKeys.ItemsCatalogID)
            {
                var cbsItem = CBSModule.Get<CBSItemsModule>().GetFromCache(ItemID);
                if (cbsItem != null)
                {
                    return cbsItem.Type == ItemType.PACKS;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }
    }
}
