using System.Collections.Generic;
using PlayFab.MultiplayerModels;

namespace CBS.Models
{
    public class CBSFindMatchRequest
    {
        public string QueueName;
        public int? WaitTime;
        public string StringEqualityValue;
        public double DifferenceValue;
        public List<EntityKey> MembersToMatchWith;
    }
}
