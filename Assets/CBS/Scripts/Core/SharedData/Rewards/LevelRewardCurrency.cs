namespace CBS
{
    public class LevelRewardCurrency
    {
        public string CurrencyCode;

        public bool Once;
        
        public DropBehavior DropBehavior;
        public float DropChance;
        
        public RewardValue ValueType;
        public int FixedValue;
        public int MinValue;
        public int MaxValue;
    }
}