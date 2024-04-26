
namespace CBS.Models
{
    public class CBSPurchaseAvatarResult : CBSBaseResult
    {
        public string PurchasedAvatarID;
        public AvatarsTableWithStates UpdatedStates;
        public CBSPrice AvatarPrice;
    }
}
