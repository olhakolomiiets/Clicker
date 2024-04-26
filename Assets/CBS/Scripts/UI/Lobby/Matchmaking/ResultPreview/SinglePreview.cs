using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using UnityEngine;

namespace CBS.UI.Matchmaking
{
    public class SinglePreview : MonoBehaviour, IMatchmakingPreview
    {
        [SerializeField]
        private Transform Root;

        private MatchmakingPrefabs Prefabs { get; set; }

        private void Awake()
        {
            Prefabs = CBSScriptable.Get<MatchmakingPrefabs>();
        }

        public void Draw(CBSStartMatchResult result)
        {
            var players = result.Players;
            var playerPrefab = Prefabs.SinglePlayer;
            Root.Clear();

            foreach (var player in players)
            {
                var playerUI = Instantiate(playerPrefab, Root);
                playerUI.GetComponent<MatchmakingPreviewPlayer>().DrawUser(player);
            }
        }

        // button click
        public void StartMatch()
        {
            gameObject.SetActive(false);
        }
    }
}
