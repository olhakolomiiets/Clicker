

using PlayFab.ClientModels;
using System.Collections.Generic;

namespace CBS.Models
{
    public class GrantRewardResult
    {
        public RewardObject OriginReward;
        public List<ItemInstance> GrantedInstances;
        public Dictionary<string, uint> GrantedCurrencies;
    }
}
