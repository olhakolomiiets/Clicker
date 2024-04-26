
using System.Text;

namespace CBS.Utils
{
    public static class StoreUtils
    {
        public static string GetStoreItemID(string storeID, string itemID)
        {
            var sBuilder = new StringBuilder();
            sBuilder.Append(storeID);
            sBuilder.Append(itemID);
            return sBuilder.Replace(" ", "").ToString();
        }
    }
}
