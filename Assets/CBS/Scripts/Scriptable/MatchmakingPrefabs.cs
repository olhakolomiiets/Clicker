using UnityEngine;
using UnityEngine.Serialization;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "MatchmakingPrefabs", menuName = "CBS/Add new Matchmaking Prefabs")]
    public class MatchmakingPrefabs : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/MatchmakingPrefabs";

        public GameObject MatchmakingWindow;
        public GameObject MatchmakingQueue;
        public GameObject WaitOpponentWindow;
        public GameObject DuelPreview;
        public GameObject SinglePreview;
        public GameObject TeamPreview;
        public GameObject SinglePlayer;
        public GameObject TeamLayer;
    }
}
