using CBS.Core;
using CBS.Models;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class BattlePassLevelSlot : MonoBehaviour, IScrollableItem<BattlePassLevelInfo>
    {
        private readonly float DisabledAlpha = 0;
        private readonly float LockAlpha = 0.6f;
        private readonly float DefaultAlpha = 1f;

        private readonly float ExpandWidthPerReward = 190f;

        [SerializeField]
        private CanvasGroup PremuimGroup;
        [SerializeField]
        private CanvasGroup DefaultGroup;
        [SerializeField]
        private Text LevelLabel;
        [SerializeField]
        private Text ExpToNextLevel;
        [SerializeField]
        private Slider ExpSlider;
        [SerializeField]
        private BattlePassRewardDrawer PremiumRewardDrawer;
        [SerializeField]
        private BattlePassRewardDrawer DefaultRewardDrawer;
        [SerializeField]
        private GameObject CollectPremiumButton;
        [SerializeField]
        private GameObject CollectDefaultButton;
        [SerializeField]
        private Image SliderBackground;
        [SerializeField]
        private GameObject ExtraLevelFrame;
        [SerializeField]
        private GameObject TimeLocker;
        [SerializeField]
        private Text TimeLablel;
        [SerializeField]
        private Color DefaultLineColor;
        [SerializeField]
        private Color ExtraLineColor;

        private RectTransform Rect { get; set; }
        private float DefaultFrameWith { get; set; }

        private string BattlePassID { get; set; }
        private int LevelIndex { get; set; }
        private bool IsTimerSlot { get; set; }
        private DateTime? TimerDate { get; set; }

        public BattlePassLevelInfo LevelInfo { get; private set; }

        private void Awake()
        {
            Rect = GetComponent<RectTransform>();
            DefaultFrameWith = Rect.sizeDelta.x;
        }

        private void OnDisable()
        {
            //SetTimerActivity(false);
        }

        public void Display(BattlePassLevelInfo data)
        {
            LevelInfo = data;
            var premiumReward = data.GetReward(BattlePassRewardType.PREMIUM);
            var defaultReward = data.GetReward(BattlePassRewardType.DEFAULT);
            BattlePassID = data.BattlePassID;
            LevelIndex = data.LevelIndex;
            // draw premium
            if (premiumReward == null)
            {
                PremuimGroup.alpha = DisabledAlpha;
            }
            else
            {
                PremuimGroup.alpha = data.RewardIsActive(BattlePassRewardType.PREMIUM) ? DefaultAlpha : LockAlpha;
                CollectPremiumButton.SetActive(data.IsRewardAvailableToCollect(BattlePassRewardType.PREMIUM));
                PremiumRewardDrawer.Display(premiumReward);
                PremiumRewardDrawer.MarkAsRewarded(data.IsRewardCollected(BattlePassRewardType.PREMIUM));
                PremiumRewardDrawer.MarkAsLocked(data.IsRewardLocked(BattlePassRewardType.PREMIUM));
            }
            // draw default
            if (defaultReward == null)
            {
                DefaultGroup.alpha = DisabledAlpha;
            }
            else
            {
                DefaultGroup.alpha = data.RewardIsActive(BattlePassRewardType.DEFAULT) ? DefaultAlpha : LockAlpha;
                CollectDefaultButton.SetActive(data.IsRewardAvailableToCollect(BattlePassRewardType.DEFAULT));
                DefaultRewardDrawer.Display(defaultReward);
                DefaultRewardDrawer.MarkAsRewarded(data.IsRewardCollected(BattlePassRewardType.DEFAULT));
                DefaultRewardDrawer.MarkAsLocked(data.IsRewardLocked(BattlePassRewardType.DEFAULT));
            }
            // draw exp
            ExpSlider.maxValue = data.ExpStep;
            if (data.IsCurrent())
            {
                ExpToNextLevel.gameObject.SetActive(true);
                ExpSlider.value = data.ExpOfCurrentLevel;
                ExpToNextLevel.text = string.Format("{0}/{1}", data.ExpOfCurrentLevel.ToString(), data.ExpStep);
            }
            else if (data.IsPassed())
            {
                ExpSlider.value = data.ExpStep;
                ExpToNextLevel.gameObject.SetActive(false);
            }
            else
            {
                ExpSlider.value = 0;
                ExpToNextLevel.gameObject.SetActive(false);
            }
            ExpSlider.gameObject.SetActive(!data.IsLast);

            // draw level
            LevelLabel.text = data.LevelIndex.ToString();
            // draw extra
            ExtraLevelFrame.SetActive(data.IsExtraLevel);
            SliderBackground.color = data.IsExtraLevel ? ExtraLineColor : DefaultLineColor;
            // fix width
            var premiumRewardCount = premiumReward == null ? 0 : premiumReward.GetPositionCount();
            var defaultRewardCount = defaultReward == null ? 0 : defaultReward.GetPositionCount();
            var maxRewardCount = Mathf.Max(premiumRewardCount, defaultRewardCount);
            var rewardBasedWidth = maxRewardCount * ExpandWidthPerReward;
            var newWidth = Mathf.Max(DefaultFrameWith, rewardBasedWidth);
            Rect.sizeDelta = new Vector2(newWidth, Rect.sizeDelta.y);
            SetTimerActivity(false);
        }

        public void SetTimerActivity(bool val)
        {
            IsTimerSlot = val;
            TimeLocker.SetActive(val);
            if (!IsTimerSlot)
            {
                TimerDate = null;
            }
            else
            {
                TimerDate = LevelInfo.GetLimitDate();
            }
        }

        private void LateUpdate()
        {
            if (IsTimerSlot && TimerDate != null)
            {
                TimeLablel.text = BattlePassUtils.GetRewardLimitLabel(TimerDate.GetValueOrDefault());
            }
        }

        // button click
        public void GrantPremiumReward()
        {
            CBSModule.Get<CBSBattlePassModule>().GrantAwardToProfile(BattlePassID, LevelIndex, true, OnReciveReward);
        }

        public void GrantDefaultReward()
        {
            CBSModule.Get<CBSBattlePassModule>().GrantAwardToProfile(BattlePassID, LevelIndex, false, OnReciveReward);
        }

        // event
        private void OnReciveReward(CBSGrantAwardToPlayerResult result)
        {
            if (result.IsSuccess)
            {
                var isPremiumReward = result.IsPremium;
                if (isPremiumReward)
                    LevelInfo.CollectReward(BattlePassRewardType.PREMIUM);
                else
                    LevelInfo.CollectReward(BattlePassRewardType.DEFAULT);
                Display(LevelInfo);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
