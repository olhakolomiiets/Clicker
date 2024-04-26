using System;
using System.Text;

namespace CBS.Models
{
    [Serializable]
    public class BattlePassTicket : BattlePassInstanceAccess
    {
        private static readonly string CatalogPrefix = "cbsbp.";

        public string ID;
        public string BattlePassID;
        public string DisplayName;
        public string Description;

        public bool OverrideExpMultiply;
        public bool SkipLevel;
        public int SkipLevelCount;

        public CBSPrice Price;
        public CBSPurchaseType PurchaseType;

        public string GetCatalogID()
        {
            var sBuilder = new StringBuilder();
            sBuilder.Append(CatalogPrefix);
            sBuilder.Append(BattlePassID);
            sBuilder.Append(".");
            sBuilder.Append(ID);

            return sBuilder.ToString();
        }
    }
}
