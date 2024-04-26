using CBS.Models;
using CBS.Scriptable;
using UnityEngine;

namespace CBS.UI
{
    public class MatchmakingWindow : MonoBehaviour
    {
        [SerializeField]
        private MatchmakingScroller Scroller;

        private IMatchmaking Matchmaking { get; set; }
        private MatchmakingPrefabs Prefabs { get; set; }

        private void Awake()
        {
            Matchmaking = CBSModule.Get<CBSMatchmakingModule>();
            Prefabs = CBSScriptable.Get<MatchmakingPrefabs>();
        }

        private void OnEnable()
        {
            Scroller.HideAll();
            Matchmaking.GetMatchmakingQueuesList(OnGetQueueList);
        }

        private void OnGetQueueList(CBSGetMatchmakingListResult result)
        {
            if (result.IsSuccess)
            {
                var queuesList = result.Queues;
                var listPrefab = Prefabs.MatchmakingQueue;
                Scroller.Spawn(listPrefab, queuesList);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
