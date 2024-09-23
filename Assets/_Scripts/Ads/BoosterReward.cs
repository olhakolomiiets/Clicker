using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Firebase.Analytics;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System;

public class BoosterReward : MonoBehaviour
{
    #region EDITOR FIELDS
    
    [Space(10)]
    [SerializeField] private Button buttonReward;
    [SerializeField] private GameObject getBoosterForAdsWindow;
    [SerializeField] private GameObject _button;
    [SerializeField] private TextMeshProUGUI _coinsRewardTxt;

    [Space(10)]
    [SerializeField] private GameObject _boosterTimer;
    [SerializeField] private TextMeshProUGUI _timerTxt;
    [SerializeField] private int _boosterTime;

    [Space(10)]
    [SerializeField] private List<ItemData> _creationItemsDataList;

    [Space(10)]
    [SerializeField] private GoogleMobileAds.Sample.RewardedAdController _adController;

    #endregion

    #region UNITY EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent, RewardedAdLoadedEvent, RewardedAdLoadedWithErrorEvent, OnBoosterRewardEarned, OnBoosterRewardReceived;

    #endregion

    #region PRIVATE FIELDS

    private bool _rewardedAdUsed;

    private DateTime startTime;
    private DateTime endTime;

    #endregion

    private void OnEnable()
    {
        _rewardedAdUsed = false;
        _adController.OnUserEarnedRewardEvent.AddListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.AddListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.AddListener(RewardedAdWithError);
    }

    public void UserEarnedReward()
    {
        for (int i = 0; i < _creationItemsDataList.Count; i++)
        {
            _creationItemsDataList[i].MultiplyItemBaseIncomeBy2();
        }
        OnBoosterRewardEarned?.Invoke();
        SetBoosterTimer();
        _button.SetActive(false);
        _boosterTimer.SetActive(true);

        FirebaseAnalytics.LogEvent(name: "booster_for_ads");

        buttonReward.interactable = true;
        getBoosterForAdsWindow.SetActive(false);
        _rewardedAdUsed = true;
    }

    private void SetBoosterTimer()
    {
        DateTime now = DateTime.Now;
        int day = now.Day;
        int hour = now.Hour;
        int minute = now.Minute;
        int second = now.Second;

        startTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        endTime = startTime.AddMinutes(_boosterTime);

        StartCoroutine(UpdateCoinsBoosterTimer());
    }

    IEnumerator UpdateCoinsBoosterTimer()
    {
        while (true)
        {
            TimeSpan timeRemaining = endTime - DateTime.Now;

            _timerTxt.text = string.Format("{0:D2}:{1:D2}", timeRemaining.Minutes, timeRemaining.Seconds);

            if (timeRemaining.Ticks <= 0)
            {
                DisableCoinsBooster();                
                yield break;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private void DisableCoinsBooster()
    {
        for (int i = 0; i < _creationItemsDataList.Count; i++)
        {
            _creationItemsDataList[i].DivideItemBaseIncomeBy2();
        }
        OnBoosterRewardReceived?.Invoke();
        _boosterTimer.SetActive(false);
        this.gameObject.SetActive(false);
    }

    public void GetCoinsBooster()
    {
        buttonReward.interactable = false;
        //buttonReward.GetComponentInChildren<Text>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";

        _adController.LoadAd();
    }

    public void ShowRewardedAd()
    {
        _adController.ShowAd();
    }

    public void RewardedAdWithError()
    {
        //buttonReward.GetComponentInChildren<Text>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("RewardedAdError")}";
    }

    private void OnDisable()
    {
        _adController.OnUserEarnedRewardEvent.RemoveListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.RemoveListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.RemoveListener(RewardedAdWithError);
    }
}