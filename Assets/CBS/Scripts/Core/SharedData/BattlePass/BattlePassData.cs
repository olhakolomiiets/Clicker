using CBS.Models;
using System;
using System.Collections.Generic;

namespace CBS
{
    [Serializable]
    public class BattlePassData
    {
        public List<BattlePassInstance> Instances = new List<BattlePassInstance>();
    }
}
