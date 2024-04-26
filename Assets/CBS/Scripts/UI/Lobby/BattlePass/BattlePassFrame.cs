using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class BattlePassFrame : MonoBehaviour
    {
        [SerializeField]
        private Text Title;
        [SerializeField]
        private Text Time;
        [SerializeField]
        private Text CurrentLevel;
        [SerializeField]
        private Text CurrentExp;
        [SerializeField]
        private BattlePassBadge Badge;
        [SerializeField]
        private GameObject LockBlock;

        private string BattlePassID { get; set; }
        private DateTime? EndDate;

        private BattlePassPrefabs PassPrefabs { get; set; }
        private RectTransform Rect { get; set; }

        private void Awake()
        {
            PassPrefabs = CBSScriptable.Get<BattlePassPrefabs>();
            Rect = GetComponent<RectTransform>();
        }

        public void Draw(BattlePassUserInfo passUserInfo)
        {
            BattlePassID = passUserInfo.BattlePassID;
            EndDate = passUserInfo.EndDate;
            Title.text = passUserInfo.BattlePassName;
            CurrentLevel.text = passUserInfo.PlayerLevel.ToString();
            CurrentExp.text = passUserInfo.ExpOfCurrentLevel.ToString() + "/" + passUserInfo.ExpStep.ToString();
            Badge.UpdatePassBadge(passUserInfo.RewardBadgeCount);
            LockBlock.SetActive(!passUserInfo.IsActive);
            Rect.SetAsFirstSibling();
        }

        public void OpenPassWindow()
        {
            var prefab = PassPrefabs.BattlePassWindow;
            var windowObject = UIView.ShowWindow(prefab);
            windowObject.GetComponent<BattlePassWindow>().Load(BattlePassID);
        }

        private void LateUpdate()
        {
            if (EndDate != null)
            {
                Time.text = BattlePassUtils.GetFrameTimeLabel(EndDate.GetValueOrDefault());
            }
        }
    }
}
