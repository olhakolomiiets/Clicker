using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class BattlePassFrameLoader : MonoBehaviour
    {
        [SerializeField]
        private Transform Root;

        private IBattlePass BattlePass { get; set; }
        private BattlePassPrefabs PassPrefabs { get; set; }

        private Dictionary<int, BattlePassFrame> FramePool;

        private void Awake()
        {
            BattlePass = CBSModule.Get<CBSBattlePassModule>();
            PassPrefabs = CBSScriptable.Get<BattlePassPrefabs>();
            FramePool = new Dictionary<int, BattlePassFrame>();
        }

        private void OnEnable()
        {
            HideAllFrames();
            LoadAllFrames();
            BattlePass.OnExpirienceAdded += OnAddExpirience;
            BattlePass.OnRewardRecived += OnRewardRecived;
            BattlePass.OnTicketPurchased += OnPremiumAccessGranted;
        }

        private void OnDisable()
        {
            BattlePass.OnExpirienceAdded -= OnAddExpirience;
            BattlePass.OnRewardRecived -= OnRewardRecived;
            BattlePass.OnTicketPurchased -= OnPremiumAccessGranted;
        }

        private void LoadAllFrames()
        {
            BattlePass.GetBattlePassStates(OnGetStates);
        }

        private void OnGetStates(CBSGetPlayerBattlePassStatesResult result)
        {
            if (result.IsSuccess)
            {
                var states = result.States;
                DrawStates(states);
            }
        }

        private void DrawStates(List<BattlePassUserInfo> states)
        {
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                var frame = GetFrameAt(i);
                frame.Draw(state);
            }
        }

        private BattlePassFrame GetFrameAt(int index)
        {
            if (FramePool.ContainsKey(index))
            {
                var frame = FramePool[index];
                frame.gameObject.SetActive(true);
                return frame;
            }
            else
            {
                var framePrefab = PassPrefabs.BattlePassFrame;
                var frameObject = Instantiate(framePrefab, Root);
                var frameComponent = frameObject.GetComponent<BattlePassFrame>();
                FramePool[index] = frameComponent;
                return frameComponent;
            }
        }

        private void HideAllFrames()
        {
            foreach (var framePair in FramePool)
                framePair.Value.gameObject.SetActive(false);
        }

        // events
        private void OnAddExpirience(string instanceID, int exp)
        {
            LoadAllFrames();
        }

        private void OnRewardRecived(GrantRewardResult reward)
        {
            LoadAllFrames();
        }

        private void OnPremiumAccessGranted(BattlePassTicket ticket)
        {
            LoadAllFrames();
        }
    }
}
