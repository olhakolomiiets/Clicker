using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class BattlePassBankDrawer : MonoBehaviour
    {
        [SerializeField]
        private BattlePassBankScroller Scroller;

        private BattlePassPrefabs PassPrefabs { get; set; }

        private void Awake()
        {
            PassPrefabs = CBSScriptable.Get<BattlePassPrefabs>();
        }

        public void Draw(List<BattlePassBankLevel> levelTree)
        {
            if (PassPrefabs == null)
                PassPrefabs = CBSScriptable.Get<BattlePassPrefabs>();
            Scroller.HideAll();
            var slotPrefab = PassPrefabs.BankSlot;
            Scroller.Spawn(slotPrefab, levelTree);
        }
    }
}
