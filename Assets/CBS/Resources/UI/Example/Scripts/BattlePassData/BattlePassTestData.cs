using System.Collections.Generic;

namespace CBS.Example
{
    public class BattlePassTestData : CBSBattlePassCustomData
    {
        public string SomeString;
        public int SomeIntData;
        public float SomeFloatData;
        public BattlePassType Type;
        public List<int> TestList;
    }

    public enum BattlePassType
    {
        SIMPLE,
        PREMIUM
    }
}
