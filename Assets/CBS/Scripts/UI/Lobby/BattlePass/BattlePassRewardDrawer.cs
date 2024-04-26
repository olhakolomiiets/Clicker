using CBS.Scriptable;
using UnityEngine;

namespace CBS.UI
{
    public class BattlePassRewardDrawer : RewardDrawer
    {
        private BattlePassPrefabs PassPrefabs { get; set; }

        protected override GameObject RewardPrefab
        {
            get
            {
                if (PassPrefabs == null)
                {
                    PassPrefabs = CBSScriptable.Get<BattlePassPrefabs>();
                }
                return PassPrefabs.RewardDrawer;
            }
        }

        public override void Init()
        {
            base.Init();
            PassPrefabs = CBSScriptable.Get<BattlePassPrefabs>();
        }

        public void MarkAsRewarded(bool isRewarded)
        {
            foreach (var rewardObject in LootPool)
            {
                rewardObject.GetComponent<BattlePassRewardIcon>().MarkAsRewarded(isRewarded);
            }
        }

        public void MarkAsLocked(bool isLocked)
        {
            foreach (var rewardObject in LootPool)
            {
                rewardObject.GetComponent<BattlePassRewardIcon>().MarkAsLocked(isLocked);
            }
        }
    }
}
