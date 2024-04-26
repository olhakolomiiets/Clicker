using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class CalendarWindow : MonoBehaviour
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Description;
        [SerializeField]
        private Image Icon;
        [SerializeField]
        private Button CollectBtn;
        [SerializeField]
        private Button PurchaseBtn;
        [SerializeField]
        private CalendarScroller PositionScroller;
        [SerializeField]
        private CalendarTitleScroller TitleScroller;
        [SerializeField]
        private ToggleGroup TitleGroup;
        [SerializeField]
        private ToggleGroup PositionGroup;

        private ICalendar Calendar { get; set; }
        private CalendarPrefabs Prefabs { get; set; }
        private CalendarInstance CurrentCalendar { get; set; }
        private CalendarPosition CurrentPosition { get; set; }
        private CalendarTitle CurrentTitle { get; set; }

        private void Awake()
        {
            Calendar = CBSModule.Get<CBSCalendarModule>();
            Prefabs = CBSScriptable.Get<CalendarPrefabs>();
        }

        private void OnEnable()
        {
            CurrentTitle = null;
            CurrentCalendar = null;
            CurrentPosition = null;
            DisplayName.text = string.Empty;
            Description.text = string.Empty;
            Icon.gameObject.SetActive(false);
            CollectBtn.gameObject.SetActive(false);
            PurchaseBtn.gameObject.SetActive(false);
            PositionScroller.HideAll();
            TitleScroller.HideAll();
            LoadAllCalendars();
        }

        private void LoadAllCalendars()
        {
            Calendar.GetAllAvailableCalendars(OnGetCalendar);
        }

        private void DrawTitles(List<CalendarInstance> titles)
        {
            var titlePrefab = Prefabs.CalendarTitle;
            var uiList = TitleScroller.Spawn(titlePrefab, titles);
            foreach (var ui in uiList)
            {
                ui.GetComponent<Toggle>().group = TitleGroup;
                ui.GetComponent<CalendarTitle>().SetSelectionAction(OnSelectTitle);
            }
            var firstTitle = uiList.FirstOrDefault();
            if (firstTitle != null)
            {
                firstTitle.GetComponent<Toggle>().isOn = true;
            }
        }

        private void DrawInstance(CalendarInstance instance)
        {
            CurrentPosition = null;
            CurrentCalendar = instance;
            CollectBtn.gameObject.SetActive(false);
            DisplayName.text = instance.DisplayName;
            Description.text = instance.Description;
            var iconSprite = instance.GetSprite();
            Icon.gameObject.SetActive(iconSprite != null);
            Icon.sprite = instance.GetSprite();

            var positions = instance.Positions;
            var slotPrefab = Prefabs.CalendarSlot;
            var canPurchase = instance.Activation == ActivationType.BY_PURCHASE && !instance.IsAvailable;
            slotPrefab.GetComponent<Toggle>().group = PositionGroup;
            var uiList = PositionScroller.Spawn(slotPrefab, positions);
            if (instance.IsAvailable)
            {
                PositionGroup.allowSwitchOff = false;
                foreach (var ui in uiList)
                {
                    ui.GetComponent<CalendarSlot>().SetSelectionAction(OnSelectPosition);
                    ui.GetComponent<CalendarSlot>().CheckActive();
                }
            }
            else
            {
                PositionGroup.allowSwitchOff = true;
                foreach (var ui in uiList)
                {
                    ui.GetComponent<Toggle>().isOn = false;
                }
            }
            PurchaseBtn.gameObject.SetActive(canPurchase);
            if (canPurchase)
            {
                CollectBtn.gameObject.SetActive(false);
                var price = instance.Price;
                var code = price.CurrencyID;
                var value = price.CurrencyValue;
                PurchaseBtn.gameObject.GetComponent<StorePurchaseButton>().Display(code, value, PurchaseRequest);
            }
        }

        private void DrawPosition(CalendarPosition position)
        {
            CurrentPosition = position;
            if (!position.Rewarded && position.CanBeRewarded)
            {
                CollectBtn.gameObject.SetActive(true);
            }
            else
            {
                CollectBtn.gameObject.SetActive(false);
            }
        }

        // buttons events
        public void OnCollectClick()
        {
            /*if (CurrentCalendar == null || CurrentPosition == null)
                return;*/
            var calendarID = CurrentCalendar.ID;
            var positionIndex = CurrentPosition.Position;
            //CollectBtn.interactable = false;
            Calendar.PickupReward(calendarID, positionIndex, OnRewardCollected);
        }

        // events
        private void OnGetCalendar(CBSGetAllCalendarsResult result)
        {
            if (result.IsSuccess)
            {
                var calendarInstances = result.Instances;
                DrawTitles(calendarInstances);
            }
            else
            {
                new PopupViewer().ShowStackError(result.Error);
            }
        }

        private void OnRewardCollected(CBSPickupCalendarReward result)
        {
            CollectBtn.interactable = true;
            if (result.IsSuccess)
            {
                CollectBtn.gameObject.SetActive(false);
                var updatedPosition = result.UpdatedPosition;
                var index = updatedPosition.Position;
                CurrentCalendar.UpdatePosition(updatedPosition, index);
                CurrentCalendar.BadgeCount--;
                CurrentTitle.Display(CurrentCalendar);
                DrawInstance(CurrentCalendar);
                DrawPosition(updatedPosition);
            }
            else
            {
                CollectBtn.gameObject.SetActive(true);
                new PopupViewer().ShowStackError(result.Error);
            }
        }

        private void OnSelectTitle(CalendarInstance instance, CalendarTitle title)
        {
            CurrentTitle = title;
            DrawInstance(instance);
        }

        private void OnSelectPosition(CalendarPosition position)
        {
            DrawPosition(position);
        }

        private void PurchaseRequest(string code, int value)
        {
            if (CurrentCalendar == null)
                return;
            var calendarID = CurrentCalendar.ID;

            PurchaseBtn.interactable = false;
            if (code == PlayfabUtils.REAL_MONEY_CODE)
            {
                Calendar.PurchaseCalendarWithRM(calendarID, OnPurchaseCalendarWithRM);
            }
            else
            {
                Calendar.PurchaseCalendar(calendarID, OnPurchaseCalendar);
            }
        }

        private void OnPurchaseCalendar(CBSPurchaseCalendarResult result)
        {
            PurchaseBtn.interactable = true;
            if (result.IsSuccess)
            {
                var newCalendar = result.PurchasedInstance;
                CurrentTitle.Display(newCalendar);
                DrawInstance(newCalendar);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnPurchaseCalendarWithRM(CBSPurchaseCalendarWithRMResult result)
        {
            PurchaseBtn.interactable = true;
            if (result.IsSuccess)
            {
                var newCalendar = result.PurchasedInstance;
                CurrentTitle.Display(newCalendar);
                DrawInstance(newCalendar);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
