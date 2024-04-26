using CBS.Core;
using CBS.Models;
using CBS.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class BattlePassBankSlot : MonoBehaviour, IScrollableItem<BattlePassBankLevel>
    {
        [SerializeField]
        private Text Title;
        [SerializeField]
        private GameObject Passed;
        [SerializeField]
        private GameObject Locked;
        [SerializeField]
        private RewardDrawer RewardDrawer;

        public void Display(BattlePassBankLevel data)
        {
            Title.text = BattlePassTXTHandler.GetBankSlotTitle(data.TargetIndex);
            Passed.SetActive(data.Reached);
            Locked.SetActive(!data.Available);
            RewardDrawer.Init();
            RewardDrawer.Display(data.Reward);
        }
    }
}
