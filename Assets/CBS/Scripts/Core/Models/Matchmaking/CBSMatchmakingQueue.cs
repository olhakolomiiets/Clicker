
using PlayFab.MultiplayerModels;
using System;
using System.Linq;

namespace CBS.Models
{
    [Serializable]
    public class CBSMatchmakingQueue
    {
        public string QueueName;
        public MatchmakingMode Mode;
        public int PlayersCount;

        public bool IsDuel() => PlayersCount == 2;
        public bool IsLevelEquality { get; private set; }
        public bool IsStringEquality { get; private set; }
        public bool IsLevelDifference { get; private set; }
        public bool IsValueDifference { get; private set; }

        public static CBSMatchmakingQueue FromMatchConfig(MatchmakingQueueConfig config)
        {
            var queueMode = config.Teams == null || config.Teams.Count == 0 ? MatchmakingMode.Single : MatchmakingMode.Team;
            return new CBSMatchmakingQueue
            {
                Mode = queueMode,
                QueueName = config.Name,
                PlayersCount = (int)config.MaxMatchSize,
                IsLevelEquality = config.StringEqualityRules == null ? false : config.StringEqualityRules.Any(x => x.Name == CBSConstants.LevelEqualityRuleName),
                IsStringEquality = config.StringEqualityRules == null ? false : config.StringEqualityRules.Any(x => x.Name == CBSConstants.StringEqualityRuleName),
                IsLevelDifference = config.DifferenceRules == null ? false : config.DifferenceRules.Any(x => x.Name == CBSConstants.LevelDifferenceRuleName),
                IsValueDifference = config.DifferenceRules == null ? false : config.DifferenceRules.Any(x => x.Name == CBSConstants.ValueDifferenceRuleName),
            };
        }
    }
}
