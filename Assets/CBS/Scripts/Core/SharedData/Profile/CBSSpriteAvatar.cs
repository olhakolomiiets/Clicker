
namespace CBS.Models
{
    public class CBSSpriteAvatar
    {
        public string ID;
        public bool HasLevelLimit;
        public bool Purchasable;
        public int LevelLimit;
        public CBSPrice Price;

        public CBSAvatarState ToState()
        {
            return new CBSAvatarState
            {
                ID = this.ID,
                HasLevelLimit = this.HasLevelLimit,
                Purchasable = this.Purchasable,
                LevelLimit = this.LevelLimit,
                Price = this.Price
            };
        }
    }
}
