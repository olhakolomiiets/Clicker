
using PlayFab.MultiplayerModels;
using System;

namespace CBS.Models
{
    [Serializable]
    public class CBSMatchmakingPlayer
    {
        public string ProfileID;
        public string PlayerEntityID;
        public string Team;

        public static CBSMatchmakingPlayer FromFabModel(MatchmakingPlayerWithTeamAssignment fabPlayer)
        {
            var rawAttributes = fabPlayer.Attributes.DataObject.ToString();
            var attributes = JsonPlugin.FromJson<CBSPlayerAttributes>(rawAttributes);

            return new CBSMatchmakingPlayer
            {
                ProfileID = attributes.ProfileID,
                PlayerEntityID = fabPlayer.Entity.Id,
                Team = fabPlayer.TeamId
            };
        }

        public static CBSMatchmakingPlayer FromFabModel(MatchmakingPlayer fabPlayer)
        {
            return new CBSMatchmakingPlayer
            {
                PlayerEntityID = fabPlayer.Entity.Id
            };
        }
    }
}
