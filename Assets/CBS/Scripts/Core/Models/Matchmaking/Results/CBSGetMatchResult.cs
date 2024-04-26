using PlayFab.MultiplayerModels;
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetMatchResult : CBSBaseResult
    {
        public string ArrangementString;
        public string MatchId;
        public List<CBSMatchmakingPlayer> Members;
        public List<string> RegionPreferences;
        public ServerDetails ServerDetails;
    }
}
