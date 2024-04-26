using CBS.Core;
using CBS.Models;
using CBS.Utils;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    [RequireComponent(typeof(Toggle))]
    public class CalendarSlot : MonoBehaviour, IScrollableItem<CalendarPosition>
    {
        [SerializeField]
        private Text Title;
        [SerializeField]
        private GameObject Rewarded;
        [SerializeField]
        private GameObject Missed;
        [SerializeField]
        private GameObject Active;
        [SerializeField]
        private SimpleIcon RewardDrawer;

        private CalendarPosition Position { get; set; }
        private Toggle Toggle { get; set; }
        private Action<CalendarPosition> SelectAction { get; set; }

        private void Awake()
        {
            Toggle = GetComponent<Toggle>();
            Toggle.onValueChanged.AddListener(OnToggleStateChange);
        }

        private void OnDestroy()
        {
            Toggle.onValueChanged.RemoveListener(OnToggleStateChange);
        }

        private void OnDisable()
        {
            Toggle.isOn = false;
        }

        public void Display(CalendarPosition data)
        {
            Position = data;
            var rewarded = data.Rewarded;
            var canBeRewarded = data.CanBeRewarded;
            var active = data.Active;
            var missed = data.Missed;
            var reward = data.Reward;

            Rewarded.SetActive(rewarded);
            Missed.SetActive(missed);
            Active.SetActive(active);
            Toggle.interactable = canBeRewarded;
            Title.text = data.GetPositionText();
            DrawReward(reward);
        }

        public void CheckActive()
        {
            if (Position == null || Toggle == null)
                return;
            var active = Position.Active;
            Toggle.isOn = active;
            Active.SetActive(active);
        }

        public void SetSelectionAction(Action<CalendarPosition> action)
        {
            SelectAction = action;
        }

        private void DrawReward(RewardObject reward)
        {
            if (reward == null || reward.IsEmpty())
            {
                RewardDrawer.HideIcon();
                RewardDrawer.HideValue();
            }
            else
            {
                var currency = reward.BundledVirtualCurrencies;
                var items = reward.BundledItems;
                var lootboxes = reward.Lootboxes;

                if (currency != null && currency.Count > 0)
                {
                    var defaultCurrency = currency.FirstOrDefault();
                    RewardDrawer.DrawCurrency(defaultCurrency.Key);
                    RewardDrawer.DrawValue(defaultCurrency.Value.ToString());
                }
                else if (items != null && items.Count > 0)
                {
                    var defaultItem = items.FirstOrDefault();
                    RewardDrawer.DrawItem(defaultItem);
                    RewardDrawer.HideValue();
                }
                else if (lootboxes != null && lootboxes.Count > 0)
                {
                    var defaultItem = lootboxes.FirstOrDefault();
                    RewardDrawer.DrawItem(defaultItem);
                    RewardDrawer.HideValue();
                }
            }
        }

        // events
        private void OnToggleStateChange(bool val)
        {
            if (val)
            {
                SelectAction?.Invoke(Position);
            }
        }
    }
}
